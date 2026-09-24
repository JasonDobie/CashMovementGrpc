# CashMovement gRPC Service

A C# / .NET 8 cash-movement service using ASP.NET Core gRPC, protobuf and SQL Server Express.

## Requirements

- Visual Studio 2022
- .NET 8 SDK
- SQL Server Express instance `.\SQLEXPRESS`

Database name: CashMovements

Application connection string:

Server=.\SQLEXPRESS;Database=CashMovements;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=False


## Create the database

Run the scripts against `.\SQLEXPRESS` in this order:

1. `database/01-create-database.sql` - creates `CashMovements` if it does not exist.
2. `database/02-create-schema.sql` - creates the tables, constraints and index.
3. `database/03-seed-data.sql` - creates sample accounts and movements.


## Run the gRPC host

1. Open `CashMovement.sln` in Visual Studio 2022.
2. Right-click the CashMovement.GrpcHost project and select Set as Startup Project
3. Run the database scripts.
4. Build the solution.
5. Press F5

The launch profile uses:

https://localhost:7243
http://localhost:5243


## Run the tests

### Visual Studio 2022

1. Open the solution.
2. Build the solution.
3. Open Test > Test Explorer.
4. Select Run All Tests.

RPCs:

- `SubmitMovement`
- `GetBalance`
- `ExportStatement` (server streaming)


## Duplicate and concurrency behaviour

`(AccountId, ExternalRef)` is unique in SQL Server. Identical retries return the original movement with outcome `DUPLICATE` and do not change the balance. A repeated reference with different fields returns gRPC `ALREADY_EXISTS` and does not change the balance.

## Transfers

A transfer is represented as two movements: a negative source-account movement and a positive destination-account movement.