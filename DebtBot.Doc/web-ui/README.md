# DebtBot web UI: orchestration records

This folder holds the working documents for building DebtBot's extended API, its web sign-in (Telegram Login Widget plus an optional password) and its web UI.

The work is split into tasks T1 to T9. A separate worker session runs each task. The orchestrator session ("DebtBot orchestrator") merges each finished task into the `web-ui` branch, and that branch has one draft PR to `main`.

| Path | What it is |
|---|---|
| `plan.md` | The agreed plan, the user's decisions and the task list |
| `briefs/common.md` | Rules every worker follows: git, machine limits, product decisions, mockups first, and how to wake the orchestrator |
| `briefs/task-NN.md` | The brief each worker started from. A brief is written when its task starts, so it can use the earlier reports. |
| `reports/task-NN.md` | Each worker's final report |
| `notes/` | Copies of the orchestration notes from memory (palette, how the work runs) |
| `mockups/` | Mockups the user approved, one folder per view. `palette/` holds the four palette options; Lime on graphite was chosen. |
