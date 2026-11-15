using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace DisplayKitDesigner
{
    [InitializeOnLoad]
    internal static class DisplayKitDesignerUpdater
    {
        private const string LatestVersionUrl = "https://raw.githubusercontent.com/killers0992/DisplayKitDesigner/main/latest.json";
        private const string ReleasesUrl = "https://github.com/killers0992/DisplayKitDesigner/releases";
        private const string LastCheckPreference = "DisplayKitDesigner.Updater.LastCheckUtc";
        private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(15);
        private const double SchedulerPollSeconds = 60d;

        private static UnityWebRequest versionRequest;
        private static UnityWebRequestAsyncOperation versionOperation;
        private static UnityWebRequest downloadRequest;
        private static UnityWebRequestAsyncOperation downloadOperation;
        private static string downloadVersion;
        private static bool manualCheck;
        private static bool checkedOnToolLaunch;
        private static double nextSchedulerPoll;

        [Serializable]
        private sealed class LatestVersionInfo
        {
            public string version = string.Empty;
        }

        static DisplayKitDesignerUpdater()
        {
            EditorApplication.delayCall += CheckAutomatically;
            EditorApplication.update += PollScheduledCheck;
        }

        internal static void CheckOnToolLaunch()
        {
            if (checkedOnToolLaunch || Application.isBatchMode)
                return;

            checkedOnToolLaunch = true;
            StartVersionCheck(false);
        }

        [MenuItem("Help/DisplayKIT Designer/Check for Updates")]
        private static void CheckManually()
        {
            StartVersionCheck(true);
        }

        private static void CheckAutomatically()
        {
            if (Application.isBatchMode || !IsCheckDue())
                return;

            StartVersionCheck(false);
        }

        private static void PollScheduledCheck()
        {
            if (Application.isBatchMode || EditorApplication.timeSinceStartup < nextSchedulerPoll)
                return;

            nextSchedulerPoll = EditorApplication.timeSinceStartup + SchedulerPollSeconds;
            if (IsCheckDue())
                StartVersionCheck(false);
        }

        private static bool IsCheckDue()
        {
            string stored = EditorPrefs.GetString(LastCheckPreference, string.Empty);

            return !long.TryParse(stored, out long ticks) || DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc) >= CheckInterval;
        }

        private static void StartVersionCheck(bool isManual)
        {
            if (versionOperation != null || downloadOperation != null)
                return;

            manualCheck = isManual;
            versionRequest = UnityWebRequest.Get(LatestVersionUrl);
            versionRequest.timeout = 10;
            versionOperation = versionRequest.SendWebRequest();
            EditorApplication.update += PollVersionCheck;
        }

        private static void PollVersionCheck()
        {
            if (versionOperation == null || !versionOperation.isDone)
                return;

            EditorApplication.update -= PollVersionCheck;
            EditorPrefs.SetString(LastCheckPreference, DateTime.UtcNow.Ticks.ToString());

            try
            {
                if (versionRequest.result != UnityWebRequest.Result.Success)
                {
                    ReportCheckFailure(versionRequest.error);
                    return;
                }

                LatestVersionInfo latest = JsonUtility.FromJson<LatestVersionInfo>(versionRequest.downloadHandler.text);
                if (latest == null || string.IsNullOrWhiteSpace(latest.version))
                {
                    ReportCheckFailure("latest.json does not contain a version.");
                    return;
                }

                string installedVersion = GetInstalledVersion();
                if (CompareVersions(latest.version, installedVersion) <= 0)
                {
                    if (manualCheck)
                        EditorUtility.DisplayDialog("DisplayKIT Designer", $"Version {installedVersion} is up to date.", "OK");
                    return;
                }

                int choice = EditorUtility.DisplayDialogComplex(
                    "DisplayKIT Designer update",
                    $"Version {latest.version} is available.\n\nInstalled version: {installedVersion}",
                    "Update",
                    "Release page",
                    "Later");

                if (choice == 0)
                    DownloadUpdate(latest.version);
                else if (choice == 1)
                    Application.OpenURL($"{ReleasesUrl}/tag/v{latest.version}");
            }
            catch (Exception exception)
            {
                ReportCheckFailure(exception.Message);
            }
            finally
            {
                versionRequest?.Dispose();
                versionRequest = null;
                versionOperation = null;
                manualCheck = false;
            }
        }

        private static string GetInstalledVersion()
        {
            UnityEditor.PackageManager.PackageInfo package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(DisplayKitDesignerUpdater).Assembly);

            return package?.version ?? "0.0.0";
        }

        private static int CompareVersions(string left, string right)
        {
            return ParseVersion(left).CompareTo(ParseVersion(right));
        }

        private static Version ParseVersion(string value)
        {
            string core = (value ?? "0.0.0").Split('-', '+')[0];

            if (Version.TryParse(core, out Version version))
                return version;

            return new Version(0, 0, 0);
        }

        private static void DownloadUpdate(string version)
        {
            downloadVersion = version;
            string fileName = $"DisplayKitDesigner-{version}.unitypackage";
            string url = $"{ReleasesUrl}/download/v{version}/{fileName}";
            downloadRequest = UnityWebRequest.Get(url);
            downloadRequest.timeout = 60;
            downloadOperation = downloadRequest.SendWebRequest();
            EditorApplication.update += PollDownload;
        }

        private static void PollDownload()
        {
            if (downloadOperation == null || !downloadOperation.isDone)
                return;

            EditorApplication.update -= PollDownload;
            try
            {
                if (downloadRequest.result != UnityWebRequest.Result.Success)
                {
                    string error = downloadRequest.error ?? "Unknown download error.";
                    Debug.LogError("DisplayKIT Designer update download failed: " + error);
                    EditorUtility.DisplayDialog("Update download failed", error, "OK");
                    return;
                }

                string fileName = $"DisplayKitDesigner-{downloadVersion}.unitypackage";
                string destination = Path.Combine(Path.GetTempPath(), fileName);
                File.WriteAllBytes(destination, downloadRequest.downloadHandler.data);
                UnityEditor.AssetPackage.Package.Import(destination, true);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Update failed", exception.Message, "OK");
            }
            finally
            {
                downloadRequest?.Dispose();
                downloadRequest = null;
                downloadOperation = null;
                downloadVersion = null;
            }
        }

        private static void ReportCheckFailure(string error)
        {
            Debug.LogWarning("DisplayKIT Designer update check failed: " + error);

            if (manualCheck)
                EditorUtility.DisplayDialog("Update check failed", error, "OK");
        }
    }
}
