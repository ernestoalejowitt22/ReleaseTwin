// Every test in this assembly launches a real Chromium via Playwright. Running two test classes in
// parallel means two Playwright driver processes fighting for the same machine — flaky, and slower
// overall. Serialize the whole assembly (a single browser-driving class was already serial by
// default; ui-session-video added a second class).
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
