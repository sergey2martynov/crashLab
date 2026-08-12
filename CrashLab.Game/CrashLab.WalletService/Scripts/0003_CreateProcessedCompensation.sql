CREATE TABLE processed_compensation (
                                        id UUID PRIMARY KEY,
                                        account_id UUID,
                                        amount NUMERIC,
                                        direction TEXT
);