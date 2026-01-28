# OpenVoice Lab Worker API

Base URL: `http://127.0.0.1:{port}`

## Health
- `GET /health`

## System
- `GET /system`

## Models
- `GET /models/status`
  - Returns `models: [{ model_id, kind, size, status, downloaded_bytes, total_bytes, progress, path, error }]`
- `POST /models/download` `{ model_id }`
- `GET /models/download/events?model_id=...` (SSE)
  - Emits `{ pct, stage, downloaded_bytes, total_bytes, error }`

## Voices
- `GET /voices`
- `POST /voices/clone` (multipart)
  - fields: `name`, `model_size`, `backend`, `keep_ref_audio`, `consent`, `ref_text` (optional), `audio`
- `POST /voices/design` `{ name, description, seed_text, model_size, backend }`
- `PATCH /voices/{voice_id}` `{ name, tags }`
- `DELETE /voices/{voice_id}`

## TTS
- `POST /tts`
  - Body accepts `pronunciation_profile_id` to apply a profile.
- `POST /tts/stream`
  - Returns 16-bit PCM LE and `X-Sample-Rate` header.

## Projects & History
- `GET /projects`
- `POST /projects` `{ name }`
- `GET /history?limit=&project_id=&q=`
- `GET /history/{job_id}`
  - History entries include `pronunciation_profile_id` when set.

## Pronunciation
- `GET /pronunciation/profiles`
- `POST /pronunciation/profiles` `{ name }`
- `PUT /pronunciation/profiles/{id}` `{ entries: [{ from, to }] }`
- `DELETE /pronunciation/profiles/{id}`

## Data
- `DELETE /data`
