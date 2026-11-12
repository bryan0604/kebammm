using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Kebamm > Git > Commit... : one-click local commit from the editor.
/// Stages everything except the skip list below, then commits with your message.
/// Never pushes.
/// </summary>
public class KebammGitCommitWindow : EditorWindow
{
    // Always left out of commits.
    static readonly string[] SkipPaths =
    {
        "Assets/_Recovery",
        "Assets/_Recovery.meta",
        "ProjectSettings/ProjectSettings.asset",
    };

    string _message = "";
    string _status = "";
    string _result = "";
    string _branch = "";
    Vector2 _scroll;

    [MenuItem("Kebamm/Git/Commit...")]
    static void Open()
    {
        var w = GetWindow<KebammGitCommitWindow>("Kebamm Commit");
        w.minSize = new Vector2(420f, 360f);
        w.Refresh();
    }

    void OnFocus() => Refresh();

    void Refresh()
    {
        _branch = RunGit("rev-parse --abbrev-ref HEAD", out _).Trim();
        string raw = RunGit("status --porcelain -uall", out int code);
        if (code != 0) { _status = raw; return; }
        var lines = new List<string>();
        foreach (var line in raw.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string l = line.TrimEnd('\r');
            string path = l.Length > 3 ? l.Substring(3).Trim('"') : l;
            lines.Add(IsSkipped(path) ? $"{l}   (skipped)" : l);
        }
        _status = lines.Count == 0 ? "Nothing to commit." : string.Join("\n", lines);
        Repaint();
    }

    static bool IsSkipped(string path)
    {
        foreach (var s in SkipPaths)
            if (path == s || path.StartsWith(s + "/")) return true;
        return false;
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Branch", string.IsNullOrEmpty(_branch) ? "?" : _branch);
        EditorGUILayout.LabelField("Changes (skipped files stay out of the commit)", EditorStyles.boldLabel);
        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
        EditorGUILayout.SelectableLabel(_status, EditorStyles.textArea, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();

        EditorGUILayout.LabelField("Commit message", EditorStyles.boldLabel);
        _message = EditorGUILayout.TextArea(_message, GUILayout.MinHeight(48f));

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Refresh", GUILayout.Height(28f))) Refresh();
        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_message)))
        {
            if (GUILayout.Button("Commit (local, no push)", GUILayout.Height(28f))) Commit();
        }
        EditorGUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(_result))
            EditorGUILayout.HelpBox(_result, MessageType.Info);
    }

    void Commit()
    {
        AssetDatabase.SaveAssets();

        string excludes = "";
        foreach (var s in SkipPaths) excludes += $" \":(exclude){s}\"";
        string addOut = RunGit("add -A -- ." + excludes, out int addCode);
        if (addCode != 0) { _result = "git add failed:\n" + addOut; Refresh(); return; }

        // Make sure skipped files are not staged from an earlier manual add.
        foreach (var s in SkipPaths) RunGit($"reset -q -- \"{s}\"", out _);

        string staged = RunGit("diff --cached --name-only", out _).Trim();
        if (string.IsNullOrEmpty(staged)) { _result = "Nothing staged to commit."; Refresh(); return; }

        string msgFile = Path.Combine(Path.GetTempPath(), "kebamm_commit_msg.txt");
        File.WriteAllText(msgFile, _message.Trim() + "\n");
        string outText = RunGit($"commit -F \"{msgFile}\"", out int code);
        File.Delete(msgFile);

        if (code == 0)
        {
            string hash = RunGit("rev-parse --short HEAD", out _).Trim();
            _result = $"Committed {hash} on {_branch}. Not pushed.";
            _message = "";
            UnityEngine.Debug.Log($"[Kebamm] {_result}");
        }
        else
        {
            _result = "Commit failed:\n" + outText;
        }
        Refresh();
    }

    static string RunGit(string args, out int exitCode)
    {
        try
        {
            var psi = new ProcessStartInfo("git", args)
            {
                WorkingDirectory = Directory.GetParent(Application.dataPath).FullName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            string o = p.StandardOutput.ReadToEnd();
            string e = p.StandardError.ReadToEnd();
            p.WaitForExit();
            exitCode = p.ExitCode;
            return string.IsNullOrEmpty(e) ? o : o + e;
        }
        catch (Exception ex)
        {
            exitCode = -1;
            return "Could not run git: " + ex.Message;
        }
    }
}