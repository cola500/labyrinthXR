// Progress lines for scripts/labyrinth.sh test: one "[test-progress]" line when each test starts and finishes, so the
// script can show progress and name the test that hangs (a stall stops the run). Same scheme as Lilla fruktståndet.
using NUnit.Framework.Interfaces;
using UnityEngine;
using UnityEngine.TestRunner;

[assembly: TestRunCallback(typeof(Labyrinth.Tests.TestProgressEditMode))]

namespace Labyrinth.Tests
{
    public sealed class TestProgressEditMode : ITestRunCallback
    {
        int total, done;

        static void Line(string text) => Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "[test-progress] {0}", text);

        public void RunStarted(ITest testsToRun)
        {
            total = testsToRun.TestCaseCount;
            done = 0;
            Line($"run {total}");
        }

        public void RunFinished(ITestResult testResults) => Line($"finished {done}/{total}");

        public void TestStarted(ITest test)
        {
            if (!test.IsSuite) Line($"start {test.FullName}");
        }

        public void TestFinished(ITestResult result)
        {
            if (result.Test.IsSuite) return;
            done++;
            Line($"{result.ResultState.Status} {done}/{total} {result.Test.FullName}");
        }
    }
}
