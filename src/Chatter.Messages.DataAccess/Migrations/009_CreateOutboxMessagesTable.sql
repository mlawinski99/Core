CREATE TABLE public."OutboxMessages" (
                                         "Id"             UUID           PRIMARY KEY,
                                         "OccurredOnUtc"  TIMESTAMPTZ    NOT NULL,
                                         "Type"           TEXT           NOT NULL,
                                         "Content"        TEXT           NOT NULL,
                                         "CorrelationId"  TEXT           NULL,
                                         "ProcessedOn"    TIMESTAMPTZ    NULL,
                                         "IsProcessed"    BOOLEAN        NOT NULL   DEFAULT FALSE
);

CREATE INDEX "IX_OutboxMessages_Unprocessed"
    ON public."OutboxMessages" ("OccurredOnUtc")
    WHERE "ProcessedOn" IS NULL;