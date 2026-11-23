# GOS Meet integration for HRM AI — 23 November 2026

Repository: demsoft/Strive. Release branch: feature/goshrm-ai-improvements, based on develop at 8cbce118ea240daa39c762809bd97841d19d6d1c.

## Scheduled interviews

The HRM API authorizes the current employee/candidate against the existing interview and company before requesting a one-use meeting handoff. Strive provisions an immutable organization/participant roster through a dedicated server credential. Namespaced staff and candidate identities cannot collide.

The embedded meeting route redeems the handoff, stores the resulting room-scoped session only in memory, restricts parent origins to deployment configuration, and prevents that session from accessing unrelated conference/API routes. Invite links return to the authenticated HRM portal. Recording, unrestricted participant permissions and anonymous chat are excluded from HRM meetings.

Production handoffs use hashed ticket IDs in MongoDB with atomic find-and-delete redemption and a TTL cleanup index. Tests can use the isolated in-memory constructor. This does not alter any HRM SQL schema. It is not a complete multi-instance conferencing design: the existing conference key/value runtime still requires separate scale and failover validation.

Runtime fixes include the OIDC events adapter, Howler module compatibility, durable isolated RabbitMQ queues and the SFU build entry point. The dedicated HRM bridge is separate from ordinary Strive/OIDC account authentication. Existing employee chat calls remain outside this release.

## Verification and limits

Focused authorization/controller tests, browser handoff tests and React typechecking pass. The local two-browser participant test exercises real SFU audio/video using generated camera and microphone streams, separate participant sessions and an isolated Mongo/Rabbit runtime. It does not prove real iPhone, separate-network, TURN, load or production acceptance.

## Manual deployment

The user will deploy. No staging credentials, IIS routes, protected settings or HRM databases were changed as part of the source push. Keep HRM `GosMeet:RecruitmentEnabled` disabled until the dedicated service connection, exact staff/candidate origins, media networking and matching portal wrappers are installed and verified. Do not use the ordinary production Strive queue, Mongo data or SFU ports for an AI pilot. No secrets belong in source, meeting URLs or browser storage.

The staff/candidate portal wrappers are separate HRM repository changes and must be released before enabling embedded interviews.
