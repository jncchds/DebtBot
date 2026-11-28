# DebtBot receipts series

Builds the LLM receipt parse and prefill (OpenRouter, web and bot) agreed in `../series-2/receipt-parse.md`. It runs as one orchestrator session ("DebtBot orchestrator 3", id `2d61b037`) plus one worker session per task, on branch `receipts` with one draft PR to main.

| Path | What |
|---|---|
| `plan.md` | the plan, the user's decisions and the task series |
| `briefs/common.md` | rules every worker follows |
| `briefs/task-NN.md` | each task's brief |
| `reports/task-NN.md` | each worker's report |
| `notes/` | copies of the series' memory notes (how it's run, palette) |
| `mockups/<view>/` | approved mockups |

Earlier series: `../web-ui/` and `../series-2/`.
