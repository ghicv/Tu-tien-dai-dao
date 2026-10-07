using System.Collections;
using UnityEditor;
using UnityEngine;

namespace ThienDao.Editor
{
    // Runs a long editor task a slice at a time on EditorApplication.update, so the menu call returns at once
    // (the editor stays responsive, and remote callers don't time out and retry the whole task).
    public static class EditorJob
    {
        static IEnumerator _job;
        static string _label;

        public static bool Busy => _job != null;

        public static void Start(IEnumerator job, string label)
        {
            if (_job != null)
            {
                Debug.LogWarning($"[ThienDao] '{_label}' is still running; '{label}' not started.");
                return;
            }
            _job = job;
            _label = label;
            EditorApplication.update += Pump;
            Debug.Log($"[ThienDao] Started '{label}'.");
        }

        static void Pump()
        {
            var t = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                // Work for up to ~100 ms per editor tick; the job yields between small steps.
                while (t.ElapsedMilliseconds < 100)
                {
                    if (!_job.MoveNext()) { Finish(); return; }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                Finish();
            }
        }

        static void Finish()
        {
            EditorApplication.update -= Pump;
            Debug.Log($"[ThienDao] Finished '{_label}'.");
            _job = null;
            _label = null;
        }
    }
}
