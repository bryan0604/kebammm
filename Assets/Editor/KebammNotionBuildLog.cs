#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Kebamm.EditorTools
{
    /// <summary>
    /// After an Android APK build, post a log entry to the GDD APK Notion page.
    /// Token comes only from the Windows user environment variable KEBAMM_NOTION_TOKEN
    /// (never stored in the repo, never logged). Does not use the analytics
    /// Resources/Kebamm/Secrets/notion_token.txt integration, which can only see
    /// the Internal Data Performance / Level Results database.
    /// Failures never fail the Unity build or the Telegram ping.
    /// </summary>
    public static class KebammNotionBuildLog
    {
        const string PageId = "4106ca3f-e492-8156-bf46-ff92cc4f5b34";
        const string TokenEnv = "KEBAMM_NOTION_TOKEN";
        const string NotionVersion = "2026-03-11";
        const string ApiRoot = "https://api.notion.com/v1";
        const long SinglePartMaxBytes = 20L * 1024 * 1024;
        const long DefaultMaxAttachBytes = 5L * 1024 * 1024 * 1024;
        const long PartSizeBytes = 10L * 1024 * 1024;
        const string ApkContentType = "application/vnd.android.package-archive";
        const string ZipContentType = "application/zip";
        const string Placeholder = "No builds posted yet.";

        public static void Post(BuildReport report, string apkPath)
        {
            try
            {
                string token = EnvVar(TokenEnv);
                if (string.IsNullOrEmpty(token))
                {
                    Debug.Log("[KebammBuild] Notion APK log skipped (" + TokenEnv + " not set).");
                    return;
                }

                BuildSummary summary = report.summary;
                bool ok = summary.result == BuildResult.Succeeded;
                string timeLabel = FormatSgt(DateTime.UtcNow);
                string filename = Path.GetFileName(apkPath);
                string fullPath = Path.GetFullPath(apkPath);
                bool fileExists = File.Exists(apkPath);
                long bytes = 0;
                if (fileExists)
                    bytes = new FileInfo(apkPath).Length;
                else if (ok)
                    bytes = (long)summary.totalSize;
                string sizeLabel = bytes > 0
                    ? (bytes / (1024.0 * 1024.0)).ToString("0.0", CultureInfo.InvariantCulture) + " MiB"
                    : "n/a";

                string failReason = null;
                if (!ok)
                    failReason = FirstError(report, summary);

                string uploadId = null;
                string attachedName = null;
                string attachNote = null;
                if (ok && fileExists && bytes > 0)
                {
                    long maxBytes = GetMaxUploadBytes(token);
                    if (bytes > maxBytes)
                    {
                        string maxLabel = (maxBytes / (1024.0 * 1024.0)).ToString("0", CultureInfo.InvariantCulture);
                        attachNote = "APK not attached (over Notion's " + maxLabel + " MiB limit). Grab it from Builds\\Android.";
                    }
                    else if (TryUploadApkOrZip(token, apkPath, filename, out uploadId, out attachedName, out string uploadError, out bool usedZip))
                    {
                        if (usedZip)
                            attachNote = "Attached as zip because Notion rejected the .apk file type.";
                    }
                    else
                    {
                        attachNote = "APK not attached (" + uploadError + "). Grab it from Builds\\Android.";
                    }
                }
                else if (ok && !fileExists)
                {
                    attachNote = "APK file was not found on disk, so nothing was attached.";
                }

                if (string.IsNullOrEmpty(attachedName))
                    attachedName = filename;

                string heading = timeLabel + (ok ? " — Success" : " — Failed");
                var children = new List<string>();
                children.Add(Heading3(heading));
                children.Add(Bullet("Filename: " + filename));
                children.Add(Bullet("Size: " + sizeLabel));
                children.Add(Bullet("Path: " + fullPath));
                if (!string.IsNullOrEmpty(failReason))
                    children.Add(Bullet("Error: " + failReason));
                if (!string.IsNullOrEmpty(attachNote))
                    children.Add(Bullet(attachNote));
                if (!string.IsNullOrEmpty(uploadId))
                    children.Add(FileBlock(uploadId, attachedName));
                children.Add("{\"type\":\"divider\",\"divider\":{}}");

                string listJson = Get(token, ApiRoot + "/blocks/" + PageId + "/children?page_size=20");
                FindSection(listJson, out string buildsHeadingId, out string placeholderId);
                if (!string.IsNullOrEmpty(placeholderId))
                    DeleteBlock(token, placeholderId);

                var body = new StringBuilder();
                body.Append("{\"children\":[");
                body.Append(string.Join(",", children));
                body.Append("]");
                if (!string.IsNullOrEmpty(buildsHeadingId))
                {
                    body.Append(",\"position\":{\"type\":\"after_block\",\"after_block\":{\"id\":\"");
                    body.Append(buildsHeadingId);
                    body.Append("\"}}");
                }
                body.Append("}");

                string resp = Patch(token, ApiRoot + "/blocks/" + PageId + "/children", body.ToString());
                if (resp == null)
                    Debug.LogWarning("[KebammBuild] Notion APK log failed (build unaffected): empty response.");
                else
                    Debug.Log("[KebammBuild] Notion APK log posted (" + heading + ").");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KebammBuild] Notion APK log failed (build unaffected): " + e.GetType().Name + ": " + Sanitize(e.Message));
            }
        }

        static bool TryUploadApkOrZip(string token, string apkPath, string filename, out string uploadId, out string attachedName, out string error, out bool usedZip)
        {
            uploadId = null;
            attachedName = filename;
            usedZip = false;
            if (TryUploadFile(token, apkPath, filename, ApkContentType, out uploadId, out error))
                return true;

            string apkError = error;
            string zipName = filename + ".zip";
            string zipPath = Path.Combine(Path.GetTempPath(), zipName);
            try
            {
                CreateZipContaining(apkPath, filename, zipPath);
                if (TryUploadFile(token, zipPath, zipName, ZipContentType, out uploadId, out error))
                {
                    attachedName = zipName;
                    usedZip = true;
                    error = null;
                    return true;
                }

                error = "apk: " + apkError + "; zip: " + error;
                return false;
            }
            catch (Exception e)
            {
                uploadId = null;
                error = "apk: " + apkError + "; zip: " + Truncate(Sanitize(e.Message), 80);
                return false;
            }
            finally
            {
                try
                {
                    if (File.Exists(zipPath))
                        File.Delete(zipPath);
                }
                catch (Exception)
                {
                }
            }
        }

        static void CreateZipContaining(string sourcePath, string entryName, string zipPath)
        {
            if (File.Exists(zipPath))
                File.Delete(zipPath);

            using (FileStream zipStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                ZipArchiveEntry entry = archive.CreateEntry(entryName, System.IO.Compression.CompressionLevel.NoCompression);
                using (Stream entryStream = entry.Open())
                using (FileStream input = File.OpenRead(sourcePath))
                    input.CopyTo(entryStream);
            }
        }

        static bool TryUploadFile(string token, string path, string filename, string contentType, out string uploadId, out string error)
        {
            uploadId = null;
            error = "upload failed";
            try
            {
                long bytes = new FileInfo(path).Length;
                if (bytes <= 0)
                {
                    error = "file is empty";
                    return false;
                }

                bool multi = bytes > SinglePartMaxBytes;
                int parts = 1;
                if (multi)
                    parts = (int)((bytes + PartSizeBytes - 1) / PartSizeBytes);

                string createBody = multi
                    ? "{\"mode\":\"multi_part\",\"number_of_parts\":" + parts.ToString(CultureInfo.InvariantCulture) +
                      ",\"filename\":\"" + Esc(filename) + "\",\"content_type\":\"" + Esc(contentType) + "\"}"
                    : "{\"mode\":\"single_part\",\"filename\":\"" + Esc(filename) +
                      "\",\"content_type\":\"" + Esc(contentType) + "\"}";

                string created = PostJson(token, ApiRoot + "/file_uploads", createBody, TimeSpan.FromSeconds(20));
                uploadId = FirstJsonString(created, "id");
                if (string.IsNullOrEmpty(uploadId))
                {
                    error = "Notion did not return an upload id";
                    return false;
                }

                if (multi)
                {
                    for (int part = 1; part <= parts; part++)
                    {
                        long offset = (part - 1L) * PartSizeBytes;
                        int length = (int)Math.Min(PartSizeBytes, bytes - offset);
                        if (!SendFilePart(token, uploadId, path, filename, contentType, part, offset, length, out error))
                        {
                            uploadId = null;
                            return false;
                        }
                    }

                    PostJson(token, ApiRoot + "/file_uploads/" + uploadId + "/complete", "{}", TimeSpan.FromSeconds(30));
                }
                else if (!SendFilePart(token, uploadId, path, filename, contentType, 0, 0, bytes, out error))
                {
                    uploadId = null;
                    return false;
                }

                error = null;
                return true;
            }
            catch (Exception e)
            {
                uploadId = null;
                error = Truncate(Sanitize(e.Message), 120);
                return false;
            }
        }

        static bool SendFilePart(string token, string uploadId, string path, string filename, string contentType, int partNumber, long offset, long length, out string error)
        {
            error = "upload failed";
            string sendUrl = ApiRoot + "/file_uploads/" + uploadId + "/send";
            using (var http = NotionClient(token, TimeSpan.FromSeconds(90)))
            using (var form = new MultipartFormDataContent())
            using (var stream = OpenSlice(path, offset, length))
            {
                var fileContent = new StreamContent(stream);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                form.Add(fileContent, "file", filename);
                if (partNumber > 0)
                    form.Add(new StringContent(partNumber.ToString(CultureInfo.InvariantCulture)), "part_number");

                HttpResponseMessage resp = http.PostAsync(sendUrl, form).GetAwaiter().GetResult();
                string body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (!resp.IsSuccessStatusCode)
                {
                    error = "HTTP " + (int)resp.StatusCode;
                    string snippet = ResponseSnippet(body);
                    if (!string.IsNullOrEmpty(snippet))
                        error += " " + snippet;
                    return false;
                }
            }

            error = null;
            return true;
        }

        static Stream OpenSlice(string path, long offset, long length)
        {
            var buffer = new byte[(int)length];
            using (FileStream fs = File.OpenRead(path))
            {
                fs.Position = offset;
                int read = 0;
                while (read < buffer.Length)
                {
                    int n = fs.Read(buffer, read, buffer.Length - read);
                    if (n <= 0)
                        break;
                    read += n;
                }
            }

            return new MemoryStream(buffer, writable: false);
        }

        static long GetMaxUploadBytes(string token)
        {
            try
            {
                string json = Get(token, ApiRoot + "/users/me");
                Match m = Regex.Match(json ?? "", @"""max_file_upload_size_in_bytes""\s*:\s*(\d+)");
                if (m.Success && long.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed) && parsed > 0)
                    return parsed;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KebammBuild] Could not read Notion upload limit (using 5 GiB fallback): " + Sanitize(e.Message));
            }

            return DefaultMaxAttachBytes;
        }

        static string FirstError(BuildReport report, BuildSummary summary)
        {
            string firstError = report.steps
                .SelectMany(step => step.messages)
                .Where(m => m.type == LogType.Error || m.type == LogType.Exception)
                .Select(m => m.content)
                .FirstOrDefault();
            if (string.IsNullOrEmpty(firstError))
                firstError = "Result=" + summary.result + ", errors=" + summary.totalErrors;
            firstError = firstError.Split('\n')[0].Trim();
            if (firstError.Length > 300)
                firstError = firstError.Substring(0, 300) + "...";
            return firstError;
        }

        static string FormatSgt(DateTime utc)
        {
            TimeZoneInfo tz;
            try
            {
                tz = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Singapore");
            }
            DateTime sgt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz);
            return sgt.ToString("d MMM yyyy HH:mm", CultureInfo.InvariantCulture) + " SGT";
        }

        static void FindSection(string childrenJson, out string buildsHeadingId, out string placeholderId)
        {
            buildsHeadingId = null;
            placeholderId = null;
            if (string.IsNullOrEmpty(childrenJson))
                return;

            string[] parts = Regex.Split(childrenJson, @"""object""\s*:\s*""block""");
            for (int i = 1; i < parts.Length; i++)
            {
                Match idMatch = Regex.Match(parts[i], @"""id""\s*:\s*""([0-9a-fA-F-]{36})""");
                Match typeMatch = Regex.Match(parts[i], @"""type""\s*:\s*""([^""]+)""");
                if (!idMatch.Success || !typeMatch.Success)
                    continue;
                string id = idMatch.Groups[1].Value;
                string type = typeMatch.Groups[1].Value;
                var texts = new StringBuilder();
                foreach (Match tm in Regex.Matches(parts[i], @"""plain_text""\s*:\s*""((?:\\.|[^""])*)"""))
                    texts.Append(Unesc(tm.Groups[1].Value));
                string text = texts.ToString().Trim();

                if (buildsHeadingId == null && type.StartsWith("heading_") &&
                    string.Equals(text, "Builds", StringComparison.OrdinalIgnoreCase))
                    buildsHeadingId = id;
                if (placeholderId == null && type == "paragraph" &&
                    text.IndexOf(Placeholder, StringComparison.OrdinalIgnoreCase) >= 0)
                    placeholderId = id;
            }
        }

        static void DeleteBlock(string token, string blockId)
        {
            using (var http = NotionClient(token, TimeSpan.FromSeconds(20)))
            {
                HttpResponseMessage resp = http.DeleteAsync(ApiRoot + "/blocks/" + blockId).GetAwaiter().GetResult();
                if (!resp.IsSuccessStatusCode)
                    Debug.LogWarning("[KebammBuild] Notion placeholder delete HTTP " + (int)resp.StatusCode + " (continuing).");
            }
        }

        static string Get(string token, string url)
        {
            using (var http = NotionClient(token, TimeSpan.FromSeconds(20)))
            {
                HttpResponseMessage resp = http.GetAsync(url).GetAwaiter().GetResult();
                string body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (!resp.IsSuccessStatusCode)
                    throw new InvalidOperationException("GET HTTP " + (int)resp.StatusCode + " " + ResponseSnippet(body));
                return body;
            }
        }

        static string PostJson(string token, string url, string json, TimeSpan timeout)
        {
            using (var http = NotionClient(token, timeout))
            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            {
                HttpResponseMessage resp = http.PostAsync(url, content).GetAwaiter().GetResult();
                string body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (!resp.IsSuccessStatusCode)
                    throw new InvalidOperationException("POST HTTP " + (int)resp.StatusCode + " " + ResponseSnippet(body));
                return body;
            }
        }

        static string Patch(string token, string url, string json)
        {
            using (var http = NotionClient(token, TimeSpan.FromSeconds(20)))
            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            using (var req = new HttpRequestMessage(new HttpMethod("PATCH"), url) { Content = content })
            {
                HttpResponseMessage resp = http.SendAsync(req).GetAwaiter().GetResult();
                string body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (!resp.IsSuccessStatusCode)
                    throw new InvalidOperationException("PATCH HTTP " + (int)resp.StatusCode + " " + ResponseSnippet(body));
                return body;
            }
        }

        static HttpClient NotionClient(string token, TimeSpan timeout)
        {
            var http = new HttpClient { Timeout = timeout };
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            http.DefaultRequestHeaders.TryAddWithoutValidation("Notion-Version", NotionVersion);
            return http;
        }

        static string Heading3(string text)
        {
            return "{\"type\":\"heading_3\",\"heading_3\":{\"rich_text\":[{\"type\":\"text\",\"text\":{\"content\":\"" +
                   Esc(text) + "\"}}]}}";
        }

        static string Bullet(string text)
        {
            if (text.Length > 1900)
                text = text.Substring(0, 1900) + "...";
            return "{\"type\":\"bulleted_list_item\",\"bulleted_list_item\":{\"rich_text\":[{\"type\":\"text\",\"text\":{\"content\":\"" +
                   Esc(text) + "\"}}]}}";
        }

        static string FileBlock(string uploadId, string filename)
        {
            return "{\"type\":\"file\",\"file\":{\"type\":\"file_upload\",\"file_upload\":{\"id\":\"" +
                   Esc(uploadId) + "\"},\"name\":\"" + Esc(filename) + "\"}}";
        }

        static string FirstJsonString(string json, string field)
        {
            if (string.IsNullOrEmpty(json))
                return null;
            Match m = Regex.Match(json, "\"" + Regex.Escape(field) + @"""\s*:\s*""([^""]+)""");
            return m.Success ? m.Groups[1].Value : null;
        }

        static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }

        static string Unesc(string s)
        {
            return s.Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\");
        }

        static string Sanitize(string message)
        {
            if (string.IsNullOrEmpty(message))
                return "";
            return Regex.Replace(message, @"ntn_[A-Za-z0-9]+|secret_[A-Za-z0-9]+", "[redacted]");
        }

        static string ResponseSnippet(string body)
        {
            string s = Sanitize(body ?? "");
            s = Regex.Replace(s, @"\s+", " ").Trim();
            return Truncate(s, 160);
        }

        static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max)
                return s ?? "";
            return s.Substring(0, max) + "...";
        }

        static string EnvVar(string name)
        {
            string v = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
            return string.IsNullOrEmpty(v) ? Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process) : v;
        }
    }
}
#endif
