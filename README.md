# Moneytree
## Higgsfield Seedance 2.5 example

1. `npm install`
2. `cp .env.example .env.local` and set `HF_CREDENTIALS=key-id:key-secret`
   (`.env.local` is gitignored; an `HF_CREDENTIALS` environment variable also works and takes precedence).
3. `npm start` submits a `bytedance/seedance-2.5/text-to-video` request (billable),
   waits for it to finish and prints the video URL.
   Failed, moderated (nsfw) or canceled/timed-out requests exit with code 1.
