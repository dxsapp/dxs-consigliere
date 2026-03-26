# B5 Storage Growth And Payload Economics Evidence

- measured_at_utc: 2026-03-26T10:43:26.1866770+00:00
- journal_only_tx_observations: 32
- journal_only_raw_transaction_bytes: 512
- journal_only_journal_documents: 32
- journal_only_journal_document_bytes: 27218
- journal_only_observation_json_bytes: 8832
- journal_only_elapsed_ms: 763
- journal_only_throughput_per_sec: 41.94
- payload_backed_tx_observations: 32
- payload_backed_raw_transaction_bytes: 2048
- payload_backed_journal_documents: 32
- payload_backed_payload_documents: 32
- payload_backed_journal_document_bytes: 31604
- payload_backed_observation_json_bytes: 8832
- payload_backed_payload_hex_bytes: 131072
- payload_backed_payload_document_bytes: 139069
- payload_backed_elapsed_ms: 1122
- payload_backed_throughput_per_sec: 28.52

- note: payload growth is measured against the current Raven payload backend, which stores raw hex and does not yet provide true compressed-at-rest savings.
- note: this benchmark is meant to expose storage economics trends, not to certify final archival efficiency.

