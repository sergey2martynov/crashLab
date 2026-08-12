CREATE TABLE wallets (
                         account_id UUID PRIMARY KEY,
                         balance NUMERIC NOT NULL DEFAULT 1000,
                         version INT NOT NULL DEFAULT 0
);