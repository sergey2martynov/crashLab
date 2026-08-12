CREATE TABLE processed_cashout (
                         id UUID PRIMARY KEY,
                         amount NUMERIC NOT NULL,
                         account_id UUID NOT NULL REFERENCES wallets(account_id)
    
);