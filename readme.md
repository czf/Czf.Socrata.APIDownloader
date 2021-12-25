# DownSoda
A dotnet CLI tool for downloading from Socrata Open Data API.

## Installing
### Prerequisites
You'll first need to install the [.NET SDK](https://dotnet.microsoft.com/en-us/download).

### Install Tool from CLI
Recommend you install as a global tool

```shell
dotnet tool install -g downsoda
```

## Usage

### Help Output
```
Usage: downsoda [config arguments]

Download all rows from a Socrata hosted Open Data source using a specified resource url.  

All configuration arguments can be specified in Environment Variable, appsettings.json config, 
and/or via command line.  They are read in order and overwrite values from the previous config 
location.


Config Arguments:
  -SocrataAppToken <APPLICATION_TOKEN>                         The token to identify the application with Socrata API
  -SocrataSecretToken <APPLICATION_SECRET_TOKEN>               The token to authenticate the application 
                                                               with Socrata API
  -DataUri <SOCRATA_JSON_URL>                                  The uri of the json data source.
  -FileTargetDestination <PATH_TO_STORE_FILE>                  The location to download the data file. 
                                                               (default: current working directory)
  -FileTargetBaseName <FILENAME>                               The file name that each data file will use as a 
                                                               base template. (default: result.json)
  -SkipDownload <BOOL>                                         When true the DataUri is not downloaded. (default: false)
  -QueryPagesPerFile <NUMBER_OF_PAGES>                         The number of request results to store in one file. 
                                                               (default: 10)
  -ConnectionString <DATABASE_CONNECTIONSTRING>                The connection string to use for importing to 
                                                               MSSQL Server Database.
                                                               (default: Server=.;Database=OpenData;Trusted_Connection=True;)
  -StoredProcedureProcessJson, -sproc <STORED_PROCEDURE_NAME>  The stored procedure to execute expecting the path 
                                                               to one of the data files. (default: usp_ProcessJson)
  -Logging <CONFIGURATION_SETTINGS>                            Logging configuration see: 
                                                               https://docs.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-6.0#configure-logging
  -ImportToDatabaseEnabled <BOOL>                              When true the stored procedure specified by 
                                                               StoredProcedureProcessJson will be executed 
                                                               for each data file.  The stored procedure is expected 
                                                               to have a parameter @FilePath, representing the 
                                                               location of the data file. (default: true)
```
### Samples
A sample configuration file is included in the Samples directory of this repo.
A sample database schema and store procedure is included in the SQL example subdirectory.
## Uninstall
```shell
dotnet tool uninstall downsoda -g
```
