# 0001. C# on .NET 10 with PostgreSQL

Date: 2026-10-19 · Status: Accepted

## Context
The project targets Toronto bank and fintech backend roles. Those teams mostly run Java or
.NET. George's strongest stack is TypeScript, and he has used .NET before (ASP.NET Core and SQL
Server at Continuum Commerce Solutions).

## Decision
C# on .NET 10 (LTS), ASP.NET Core minimal APIs, EF Core with Npgsql, and PostgreSQL 17. The ops
console stays React + TypeScript.

## Consequences
- The project shows the stack those employers use, and React keeps George's primary stack
  visible.
- PostgreSQL does real work here: deferred constraint triggers (balanced journals), append-only
  triggers, and `FOR UPDATE SKIP LOCKED` for workers. SQL stays visible where it matters.
- Slower to build than NestJS would have been for George.
