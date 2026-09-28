import { config as loadEnv } from 'dotenv';
import {
  AuthenticationError,
  BadInputError,
  CredentialsMissedError,
  NotEnoughCreditsError,
  TimeoutError,
  ValidationError,
  APIError,
  createHiggsfieldClient,
} from '@higgsfield/client/v2';

// Load HF_CREDENTIALS from .env.local (server-side only). Values already set in
// the process environment take precedence, so a platform secret also works.
loadEnv({ path: '.env.local', quiet: true });

const MODEL = 'bytedance/seedance-2.5/text-to-video';

function getCredentials(): string {
  const credentials = process.env.HF_CREDENTIALS?.trim();
  if (!credentials || credentials.includes('YOUR_KEY_ID')) {
    throw new CredentialsMissedError();
  }
  if (credentials.split(':').length !== 2) {
    // Do not echo the value: it is a secret.
    throw new Error('HF_CREDENTIALS must be in "key-id:key-secret" format.');
  }
  return credentials;
}

async function main(): Promise<number> {
  const client = createHiggsfieldClient({
    credentials: getCredentials(),
    // Video generation can take several minutes; the SDK default is 5 minutes.
    maxPollTime: 15 * 60 * 1000,
    pollInterval: 5000,
  });

  console.log(`Submitting ${MODEL} request...`);
  const result = await client.subscribe(MODEL, {
    input: {
      prompt: 'A cinematic scene at sunset',
      duration: 5,
      resolution: '720p',
      aspect_ratio: '16:9',
    },
    withPolling: true,
  });

  // The SDK stops polling on completed, failed or nsfw. Anything that is not a
  // completed request with a video URL is reported as a failure.
  const status: string = result.status;
  switch (status) {
    case 'completed': {
      const url = result.video?.url;
      if (!url) {
        console.error(`Request ${result.request_id} completed but returned no video URL.`);
        return 1;
      }
      console.log(`Video URL: ${url}`);
      return 0;
    }
    case 'nsfw':
      console.error(`Request ${result.request_id} was rejected by moderation (credits refunded).`);
      return 1;
    case 'failed':
      console.error(`Request ${result.request_id} failed (credits refunded).`);
      return 1;
    case 'canceled':
    case 'cancelled':
      console.error(`Request ${result.request_id} was canceled.`);
      return 1;
    default:
      console.error(`Request ${result.request_id} ended with unexpected status "${status}".`);
      return 1;
  }
}

main()
  .then((code) => process.exit(code))
  .catch((error: unknown) => {
    if (error instanceof CredentialsMissedError) {
      console.error('HF_CREDENTIALS is not set. Add it to .env.local as key-id:key-secret.');
    } else if (error instanceof AuthenticationError) {
      console.error('Authentication failed: check HF_CREDENTIALS.');
    } else if (error instanceof NotEnoughCreditsError) {
      // The SDK maps every HTTP 403 to NotEnoughCreditsError, including a 403
      // from a proxy or firewall that blocks api.higgsfield.ai.
      console.error('HTTP 403: not enough credits, or api.higgsfield.ai is blocked on this network.');
    } else if (error instanceof BadInputError || error instanceof ValidationError) {
      console.error('Invalid input:', JSON.stringify(error.responseData ?? error.message));
    } else if (error instanceof TimeoutError) {
      // Also covers a canceled request: the SDK keeps polling until maxPollTime.
      console.error('Request did not finish in time (it may have been canceled):', error.message);
    } else if (error instanceof APIError) {
      console.error(`API error ${error.statusCode ?? ''}:`, error.message);
    } else {
      console.error('Unexpected error:', error instanceof Error ? error.message : error);
    }
    process.exit(1);
  });
