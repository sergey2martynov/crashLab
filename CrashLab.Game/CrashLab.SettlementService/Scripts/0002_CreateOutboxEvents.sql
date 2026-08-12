CREATE TABLE outbox_events (
                               id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                               event_type TEXT NOT NULL,
                               payload JSONB NOT NULL,
                               created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
                               published_at TIMESTAMPTZ NULL
);

CREATE INDEX idx_outbox_events_unpublished ON outbox_events (created_at) WHERE published_at IS NULL;