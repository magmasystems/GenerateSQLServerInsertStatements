# GenerateSQLServerInsertStatements

This is a small utility that I wrote to dump the data from one or more tables in an SQL Server database. A series of INSERT statements are generated and optionally written to an output file.

This was written by Marc Adler. This code can be used freely for your own personal use. It may not be reprinted anywhere without permission of Marc Adler.

## Usage

```shell
SQLGenerateInsertStatements [-help] [-tables <tablenames>] [-database <databasename>] [-connectionstring <connstring>] [-output <outputfile>] [-deleteall] [-append] [-noidentity]");
```

The default output file is `GeneratedStatements.sql`.

If the `connectionstring` has '{db}' embedded in it, the '{d}' is replaced with the database name.

If the `outputfile` string has '{d}' embedded in it, the '{d}' is replaced with today's date.

If the `outputfile` string has '{t}' embedded in it, the '{t}' is replaced with the table name.

If `-deleteall` is included in the command line, the first statement that will be output is

```sql
DELETE FROM [dbo].[{tableName}]
```

If `-noidentity` is NOT included in the command line, this will be output as the second statement:

```sql
SET IDENTITY_INSERT [dbo].[{tableName}] ON
```

If `-append` is included in the command line, the generated statements will be appended to the existing outfile file.

Here is a sample command line
``` shell
-connectionstring "Data Source=localhost;Integrated Security=True;initial catalog=PipelineTariffTool" -database MySQLDatabase -deleteall -tables * -output "./Create {t}.sql"
```
