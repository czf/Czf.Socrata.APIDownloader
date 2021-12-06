SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- =============================================
-- Description: process json into table
-- =============================================
CREATE OR ALTER PROCEDURE [dbo].[usp_ProcessEarthquakeJson]
  @FilePath VARCHAR(max)
AS
  SET XACT_ABORT,
    NOCOUNT ON;
  BEGIN TRY;
    BEGIN TRANSACTION;
    DECLARE @JsonContent NVARCHAR(max);
    --https://stackoverflow.com/questions/13831472/using-a-variable-in-openrowset-query
    DECLARE @Sql NVARCHAR(max) = 'SELECT @JsonContentOUT = BulkColumn FROM OPENROWSET (BULK ''' + @FilePath + ''', SINGLE_CLOB) AS IMPORT;';
    EXECUTE sp_executesql
      @Sql,
      N'@JsonContentOUT NVARCHAR(MAX) OUTPUT',
      @JsonContentOUT = @JsonContent output;
    INSERT INTO earthquakedataset
                (
                            source,
                            earthquakeid,
                            version,
                            magnitude,
                            depth,
                            numberofstations,
                            region,
                            location
                )
    SELECT      b.source,
                b.earthquake_id,
                b.version,
                b.magnitude,
                b.depth,
                b.number_of_stations,
                b.region,
                CASE
                            WHEN latitude IS NULL THEN NULL
                            ELSE geography::Point(latitude,longitude,4326)
                END AS location
    FROM        Openjson(@JsonContent,N'strict $') A
    CROSS APPLY Openjson(a.value) WITH( source varchar(2) '$.source', earthquake_id varchar(20) '$.earthquake_id', version varchar(10) '$.version', magnitude decimal(5,2) '$.magnitude', depth decimal(10,3) '$.depth', number_of_stations int '$.number_of_stations', region nvarchar(1000) '$.region', latitude varchar(10) '$.location.latitude', longitude varchar(10) '$.location.longitude' ) b ;
    
    COMMIT TRANSACTION;
  END TRY
  BEGIN CATCH
  END CATCH;