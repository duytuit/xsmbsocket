IF COL_LENGTH('dbo.VietlottResults', 'EstimatedPrize') IS NULL
BEGIN
    ALTER TABLE dbo.VietlottResults
    ADD EstimatedPrize BIGINT NULL;
END;

IF COL_LENGTH('dbo.VietlottResults', 'EstimatedPrize2') IS NULL
BEGIN
    ALTER TABLE dbo.VietlottResults
    ADD EstimatedPrize2 BIGINT NULL;
END;