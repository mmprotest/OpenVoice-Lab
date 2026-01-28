# OpenVoice Lab Worker API

Base URL: `http://127.0.0.1:{port}`

## Health
- `GET /health`

## System
- `GET /system`

## Models
- `GET /models/status`
- `POST /models/download` `{ model_id }`
- `GET /models/download/events?model_id=...` (SSE)

## Voices
- `GET /voices`
- `POST /voices/clone` (multipart: name, keep_ref_audio, ref_text, audio)
- `POST /voices/design` `{ name, description, seed_text, model_size, backend }`
- `PATCH /voices/{voice_id}` `{ name, tags }`
- `DELETE /voices/{voice_id}`

## TTS
- `POST /tts`
- `POST /tts/stream`

## Projects & History
- `GET /projects`
- `POST /projects` `{ name }`
- `GET /history?limit=&project_id=&q=`
- `GET /history/{job_id}`

## Pronunciation
- `GET /pronunciation/profiles`
- `POST /pronunciation/profiles` `{ name }`
- `PUT /pronunciation/profiles/{id}` `{ entries: [{ from, to }] }`

## Data
- `DELETE /data`
