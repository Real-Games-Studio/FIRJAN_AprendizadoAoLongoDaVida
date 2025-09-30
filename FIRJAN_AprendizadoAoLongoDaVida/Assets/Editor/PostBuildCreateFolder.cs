using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

    public class PostBuildCreateFolder : IPostprocessBuildWithReport
    {
        public int callbackOrder { get; }
        public void OnPostprocessBuild(BuildReport report)
        {
            var fileLocation = report.summary.outputPath;
            var _directoryName = Path.GetDirectoryName(fileLocation);

            if (!Directory.Exists(Path.Combine(_directoryName, "contents")))
            {
            }
            else
            {
                Directory.Delete(Path.Combine(_directoryName, "contents"));
            }
            //Directory.CreateDirectory(Path.Combine(_directoryName, "contents"));
            var dirSource = new DirectoryInfo(Application.streamingAssetsPath);
            var dirTarget = new DirectoryInfo(Path.Combine(_directoryName, "contents"));
            CopyAll(dirSource, dirTarget);
        }

        private static void CopyAll(DirectoryInfo source, DirectoryInfo target)
        {
            Directory.CreateDirectory(target.FullName);

            // Copy each file into the new directory.
            foreach (FileInfo fi in source.GetFiles())
            {
                if (fi.Extension != ".meta")
                {
                    Debug.Log($@"Copying {target.FullName}\{fi.Name}");
                    fi.CopyTo(Path.Combine(target.FullName, fi.Name), true);
                }
            }

            // Copy each subdirectory using recursion.
            foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
            {
                DirectoryInfo nextTargetSubDir =
                    target.CreateSubdirectory(diSourceSubDir.Name);
                CopyAll(diSourceSubDir, nextTargetSubDir);
            }
        }
    }
