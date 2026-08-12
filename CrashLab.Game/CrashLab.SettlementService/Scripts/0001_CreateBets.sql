CREATE TABLE bets (
                      id UUID PRIMARY KEY,
                      account_id UUID NOT NULL,
                      round_id UUID NOT NULL,
                      amount NUMERIC NOT NULL,
                      status TEXT NOT NULL,
                      cash_out NUMERIC NULL,
                      created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
                      settled_at TIMESTAMPTZ NULL
);

CREATE INDEX idx_bets_round_id ON bets (round_id);