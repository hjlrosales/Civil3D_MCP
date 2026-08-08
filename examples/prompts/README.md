# Example prompts

- `typical-prompts.md` - copy-paste starters by workflow category.

Tips:

- Start with **read-only** prompts to validate the connection before anything edits
  the drawing.
- Editing tools ask for confirmation; answer the confirmation prompt (or retry with
  `confirm: true` if your client does not support elicitation).
- Long-running operations (cut/fill, corridor rebuild) support progress and
  cancellation; see `examples/json-rpc/progress-cancel.jsonl`.
