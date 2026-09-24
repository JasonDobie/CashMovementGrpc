# Solution

## Overview

This is a .NET 8 cash-movement service using ASP.NET Core gRPC, protobuf and SQL Server Express.

The solution contains separate Domain, Application, Contracts, Infrastructure and gRPC Host projects. SQL persistence uses ADO.NET so the transaction and locking behaviour is explicit.

## Data model

### Accounts

`Accounts` is the separate account table and contains the account's currency and materialised current balance.

AccountId       varchar(50)       PK
Currency        char(3)
Balance         decimal(19,2)
CreatedAtUtc    datetime2(7)


### CashMovements

Id              uniqueidentifier  PK
AccountId       varchar(50)       FK
ExternalRef     varchar(100)
Currency        char(3)
Amount          decimal(19,2)
OccurredAtUtc   datetime2(7)
Narration       nvarchar(500)
CreatedAtUtc    datetime2(7)


`(AccountId, ExternalRef)` has a unique constraint. The statement index is `(AccountId, OccurredAtUtc, Id)` with the statement fields included.


## Idempotency and concurrency

The idempotency key is `(AccountId, ExternalRef)`. SQL Server enforces this with a unique constraint.


## Balance

`Accounts.Balance` is updated in the same transaction as the movement insert. Therefore a committed movement and its balance change are atomic.

This is a materialised balance rather than recalculating `SUM(Amount)` for every balance request. The movement ledger remains the source of statement data.


## Transfers

A transfer can be represented as two movements: a negative movement on the source account and a positive movement on the destination account. A dedicated atomic transfer workflow is outside this focused slice.

## gRPC contract

The protobuf contract is in `src/CashMovement.Contracts/Protos/cash_movement.proto`. It exposes:

- `SubmitMovement`
- `GetBalance`
- `ExportStatement` (server streaming)


## Statement streaming

A statement can contain 50,000+ movements. `ExportStatement` is a server-streaming RPC.

The repository reads rows asynchronously with `SqlDataReader` and exposes them through `IAsyncEnumerable`. The gRPC service writes each row to the stream as it is read; the entire statement is never materialised into a list.

## Error handling

- `INVALID_ARGUMENT`: malformed or missing input.
- `NOT_FOUND`: account does not exist.
- `FAILED_PRECONDITION`: movement currency does not match the account currency.
- `ALREADY_EXISTS`: same idempotency key exists with conflicting fields.
- Identical retry: normal response with `DUPLICATE`.
- Client cancellation: honoured by the database reader and gRPC stream.

## Testing

Tests cover:

- balance arithmetic;
- 20 concurrent submissions of the same movement, proving only one balance change;
- identical duplicate handling;
- conflicting duplicate handling.

These choices are based on using gRPC's standard status codes to communicate the nature of the failure, rather than returning generic errors for everything.