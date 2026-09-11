using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace ActionPlatformer.Editor
{
    [InitializeOnLoad]
    public static class PrototypeValidation
    {
        private static readonly TestRunnerApi Api;
        static PrototypeValidation()
        {
            Api = ScriptableObject.CreateInstance<TestRunnerApi>();
            Api.RegisterCallbacks(new Results());
        }
        [MenuItem("Game/Prototype/Validate EditMode")]
        public static void RunEditMode() => Run(TestMode.EditMode, "ActionPlatformer.EditModeTests");
        [MenuItem("Game/Prototype/Validate PlayMode")]
        public static void RunPlayMode() => Run(TestMode.PlayMode, "ActionPlatformer.PlayModeTests");
        private static void Run(TestMode mode, string assembly)
        {
            Directory.CreateDirectory("Library/PrototypeValidation");
            SessionState.SetString("ActionPlatformer.TestMode", mode.ToString());
            Api.Execute(new ExecutionSettings(new Filter { testMode = mode, assemblyNames = new[] { assembly } }));
        }
        private sealed class Results : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                string path = "Library/PrototypeValidation/" + SessionState.GetString("ActionPlatformer.TestMode", "Tests") + ".xml";
                TestRunnerApi.SaveResultToFile(result, path);
                Debug.Log("Prototype tests: " + result.TestStatus + "; passed=" + result.PassCount + "; failed=" + result.FailCount + "; results=" + path);
            }
        }
    }
}
