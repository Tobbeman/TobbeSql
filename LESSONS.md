# TobbeSQL — Build a SQL Database from Scratch in C#

A learning project: implement a working SQL database engine, piece by piece.

## How This File Works

Each lesson covers one component. Lessons build on each other — complete them in order.
When starting a new session, tell Claude which lesson to work on. Claude will create
the files, classes, and method stubs with comments describing what each method should do.
**You write the implementation code yourself.**

After each lesson there are suggested manual tests to verify your implementation works.

Claude will also create a test project (`TobbeSQL.Tests/`, using xUnit) with tests
for each lesson. **Claude implements the test bodies** with full assertions so they
serve as a specification — you can run them immediately with `dotnet test` to check
your implementation progress.

---

## Lesson 1: Project Setup & Page Manager

**Goal:** Create the project and implement raw page I/O — the lowest level of the database.

**Concepts:**
- Databases store everything in fixed-size pages (we use 4096 bytes)
- A page is identified by its page number (0, 1, 2, ...)
- The page manager's only job is reading/writing pages to a file on disk
- Page N lives at byte offset N * 4096 in the file

**Files to create:**
- `TobbeSQL/` — console project (`dotnet new console`)
- `TobbeSQL/Storage/PageManager.cs`
- `TobbeSQL.Tests/Storage/PageManagerTests.cs`

**PageManager should have:**
- A constant `PageSize = 4096`
- Constructor that takes a file path and opens/creates the file
- `ReadPage(int pageNumber)` → returns `byte[4096]`
- `WritePage(int pageNumber, byte[] data)` → writes 4096 bytes at the right offset
- `AllocatePage()` → grows the file by one page, returns the new page number
- `PageCount` property → how many pages the file currently holds
- Implements `IDisposable` to close the file

**Tests (PageManagerTests.cs):**
- `NewFile_HasZeroPages` — a freshly created file should have PageCount == 0
- `AllocatePage_ReturnsIncrementingPageNumbers` — first allocate returns 0, second returns 1, etc.
- `WriteThenRead_ReturnsSameData` — write known bytes to a page, read it back, verify they match
- `DataSurvivesCloseAndReopen` — write data, dispose the PageManager, create a new one on the same file, read back
- `ReadPage_UnwrittenPage_ReturnsZeroes` — allocate a page without writing, reading it should return all zeroes

---

## Lesson 2: Schema & Row Serialization

**Goal:** Define table schemas and convert rows (column values) to/from bytes.

**Concepts:**
- A schema defines a table's columns: name and data type
- We support two types to start: `Integer` (4 bytes) and `Varchar` (variable length)
- Rows are serialized as: [2-byte total row length] [column1 bytes] [column2 bytes] ...
- Integers are stored as 4 bytes (BitConverter)
- Varchars are stored as: [2-byte string length] [UTF-8 bytes]
- Deserialization uses the schema to know how to interpret the bytes

**Files to create:**
- `TobbeSQL/Storage/ColumnType.cs` — enum: Integer, Varchar
- `TobbeSQL/Storage/ColumnDefinition.cs` — holds column name + type
- `TobbeSQL/Storage/Schema.cs` — holds table name + list of ColumnDefinitions
- `TobbeSQL/Storage/RowSerializer.cs`
- `TobbeSQL.Tests/Storage/RowSerializerTests.cs`

**RowSerializer should have:**
- `Serialize(Schema schema, object[] values)` → returns `byte[]`
- `Deserialize(Schema schema, byte[] data)` → returns `object[]`

**Tests (RowSerializerTests.cs):**
- `RoundTrip_IntAndVarchar` — serialize `[42, "hello"]`, deserialize, verify both values match
- `RoundTrip_EmptyString` — varchar with `""` should round-trip correctly
- `RoundTrip_NegativeInteger` — negative int like -1 should survive serialization
- `RoundTrip_LongString` — a string with 500+ characters should round-trip correctly
- `RoundTrip_MultipleIntColumns` — schema with several int columns, all values preserved

---

## Lesson 3: Slotted Page

**Goal:** Give pages internal structure so they can hold multiple variable-length rows.

**Concepts:**
- A slotted page has a header, a slot array, and row data
- Layout: `[Header | Slot0 | Slot1 | ... free space ... | RowData1 | RowData0]`
- Header (4 bytes): [2-byte slot count] [2-byte free space offset]
- Each slot (4 bytes): [2-byte row offset] [2-byte row length] — offset=0 means deleted
- Rows are inserted from the END of the page, growing backward
- Slots are added from the START (after header), growing forward
- Free space is what's left between the last slot and the last row

**Files to create:**
- `TobbeSQL/Storage/SlottedPage.cs`
- `TobbeSQL.Tests/Storage/SlottedPageTests.cs`

**SlottedPage should have:**
- Constructor that takes a `byte[4096]` (wraps an existing page)
- Static `Initialize(byte[] page)` — writes the initial header (0 slots, free space starts after header)
- `InsertRow(byte[] rowData)` → returns slot number, or -1 if page is full
- `GetRow(int slotNumber)` → returns the row's bytes, or null if deleted
- `DeleteRow(int slotNumber)` → marks the slot as deleted
- `SlotCount` property
- `FreeSpace` property — bytes available for new rows
- `GetPageData()` → returns the underlying byte array

**Tests (SlottedPageTests.cs):**
- `InsertAndGetRow_ReturnsMatchingData` — insert a row, get it back by slot number, verify bytes match
- `InsertMultipleRows_EachGetsUniqueSlot` — insert 3 rows, verify slot numbers are 0, 1, 2 and each returns its own data
- `DeleteRow_GetRowReturnsNull` — insert a row, delete it, verify GetRow returns null
- `InsertUntilFull_ReturnsNegativeOne` — keep inserting until the page runs out of space, verify -1 is returned
- `FreeSpace_DecreasesAfterInsert` — check FreeSpace before and after an insert, verify it decreased
- `SlotCount_IncrementsOnInsert` — starts at 0, goes up by 1 per insert

---

## Lesson 4: Heap File (Table Storage)

**Goal:** Combine PageManager and SlottedPage into a heap file that stores a full table.

**Concepts:**
- A heap file is a collection of pages that store rows for one table
- To insert: find a page with enough free space (or allocate a new one), insert the row
- To scan: iterate through every page, every slot, return all non-deleted rows
- A RowId identifies a row: (pageNumber, slotNumber)
- To delete: find the row by RowId, mark its slot as deleted

**Files to create:**
- `TobbeSQL/Storage/RowId.cs` — simple struct: PageNumber + SlotNumber
- `TobbeSQL/Storage/HeapFile.cs`
- `TobbeSQL.Tests/Storage/HeapFileTests.cs`

**HeapFile should have:**
- Constructor that takes a `PageManager`
- `Insert(byte[] rowData)` → returns `RowId`
- `GetRow(RowId rowId)` → returns `byte[]` or null
- `Delete(RowId rowId)`
- `Scan()` → yields all (RowId, byte[]) pairs for non-deleted rows

**Tests (HeapFileTests.cs):**
- `InsertAndGetRow_ReturnsSameData` — insert a row, use the returned RowId to get it back, verify bytes match
- `InsertManyRows_SpansMultiplePages` — insert enough rows to fill more than one page, scan and verify count matches
- `Scan_ReturnsAllInsertedRows` — insert several rows, scan, verify all are returned
- `Delete_RemovesRowFromScan` — insert 3 rows, delete the middle one, scan and verify only 2 remain
- `GetRow_AfterDelete_ReturnsNull` — insert then delete a row, verify GetRow returns null

---

## Lesson 5: Catalog (Metadata Storage)

**Goal:** Store table definitions so the database knows what tables exist and their schemas.

**Concepts:**
- The catalog keeps track of all tables: their name, schema, and which file stores their data
- For simplicity, store this as a JSON file (e.g. `catalog.json`)
- When you CREATE TABLE, an entry is added to the catalog and a new data file is created
- The catalog is loaded on startup

**Files to create:**
- `TobbeSQL/Storage/Catalog.cs`
- `TobbeSQL.Tests/Storage/CatalogTests.cs`

**Catalog should have:**
- Constructor that takes a directory path (where all database files live)
- `CreateTable(Schema schema)` — registers the table and creates its data file
- `GetTable(string tableName)` → returns Schema + the file path for that table
- `TableExists(string tableName)` → bool
- `Load()` / `Save()` — read/write the catalog file

**Tests (CatalogTests.cs):**
- `CreateTable_ThenTableExists_ReturnsTrue` — create a table, verify TableExists returns true
- `GetTable_ReturnsCorrectSchema` — create a table with known columns, get it back, verify column names and types match
- `CreateDuplicateTable_Throws` — creating a table with the same name twice should throw an exception
- `SaveAndLoad_PreservesTablesAcrossRestarts` — create tables, save, create a new Catalog on the same directory, load, verify tables are there
- `TableExists_UnknownTable_ReturnsFalse` — check for a table that was never created

---

## Lesson 6: SQL Tokenizer

**Goal:** Break a SQL string into a list of tokens.

**Concepts:**
- A token is a meaningful piece of the SQL: keyword, identifier, number, string, operator
- The tokenizer reads character by character and groups them into tokens
- Keywords: SELECT, FROM, WHERE, INSERT, INTO, VALUES, DELETE, CREATE, TABLE, INDEX, ON, AND, OR, INT, VARCHAR
- Operators: `=`, `<`, `>`, `<>`, `<=`, `>=`, `(`, `)`, `,`, `*`
- String literals: enclosed in single quotes `'hello'`
- Number literals: sequences of digits
- Identifiers: everything else (table names, column names)

**Files to create:**
- `TobbeSQL/Parser/TokenType.cs` — enum of all token types
- `TobbeSQL/Parser/Token.cs` — holds TokenType + string value
- `TobbeSQL/Parser/Tokenizer.cs`

**Tokenizer should have:**
- `Tokenize(string sql)` → returns `List<Token>`

**Tests (TokenizerTests.cs) — file: `TobbeSQL.Tests/Parser/TokenizerTests.cs`:**
- `Tokenize_Select_ProducesCorrectTokens` — tokenize `SELECT name FROM users WHERE id = 5`, verify each token type and value
- `Tokenize_Insert_ProducesCorrectTokens` — tokenize `INSERT INTO users (id, name) VALUES (1, 'Alice')`
- `Tokenize_StringLiteral_PreservesValue` — a `'hello world'` token should have value `hello world` (no quotes)
- `Tokenize_Operators_RecognizedCorrectly` — `<=`, `>=`, `<>` should each be a single token, not two
- `Tokenize_KeywordsAreCaseInsensitive` — `select`, `SELECT`, `Select` should all produce a Keyword token
- `Tokenize_CreateTable_ProducesCorrectTokens` — tokenize `CREATE TABLE users (id INT, name VARCHAR)`, covers Create, Table, Int, Varchar keywords
- `Tokenize_Delete_ProducesCorrectTokens` — tokenize `DELETE FROM users WHERE id = 5`, covers Delete keyword
- `Tokenize_CreateIndex_ProducesCorrectTokens` — tokenize `CREATE INDEX idx_id ON users (id)`, covers Index, On keywords
- `Tokenize_WhereWithAndOr_ProducesCorrectTokens` — tokenize a SELECT with AND and OR in the WHERE clause

---

## Lesson 7: SQL Parser

**Goal:** Turn a token list into an abstract syntax tree (AST).

**Concepts:**
- The parser consumes tokens left-to-right and builds a tree representing the query
- Each statement type gets its own AST node class
- The parser uses recursive descent: one method per grammar rule
- Start simple — no JOINs, no subqueries, no ORDER BY

**Files to create:**
- `TobbeSQL/Parser/Ast/CreateTableStatement.cs` — table name + column definitions
- `TobbeSQL/Parser/Ast/InsertStatement.cs` — table name + column list + values
- `TobbeSQL/Parser/Ast/SelectStatement.cs` — columns (or *) + table name + optional WHERE
- `TobbeSQL/Parser/Ast/DeleteStatement.cs` — table name + optional WHERE
- `TobbeSQL/Parser/Ast/CreateIndexStatement.cs` — index name + table name + column name
- `TobbeSQL/Parser/Ast/Expression.cs` — WHERE clause expressions (column comparisons with AND/OR)
- `TobbeSQL/Parser/Parser.cs`

**Parser should have:**
- `Parse(List<Token> tokens)` → returns a statement AST node
- Private methods for each statement type: `ParseCreateTable()`, `ParseInsert()`, `ParseSelect()`, `ParseDelete()`, `ParseCreateIndex()`
- Private methods for expression parsing: `ParseExpression()`, `ParseComparison()`

**Tests (ParserTests.cs) — file: `TobbeSQL.Tests/Parser/ParserTests.cs`:**
- `Parse_CreateTable_HasCorrectTableNameAndColumns` — parse `CREATE TABLE users (id INT, name VARCHAR)`, verify AST
- `Parse_Insert_HasTableColumnsAndValues` — parse a full INSERT statement, verify all parts
- `Parse_SelectStar_HasWildcardAndTableName` — parse `SELECT * FROM users`
- `Parse_SelectWithWhere_HasExpression` — parse `SELECT name FROM users WHERE id = 1`, verify the WHERE expression
- `Parse_Delete_HasTableAndWhereClause` — parse `DELETE FROM users WHERE id = 5`
- `Parse_CreateIndex_HasIndexNameTableAndColumn` — parse `CREATE INDEX idx_id ON users (id)`
- `Parse_WhereWithAnd_ProducesAndExpression` — parse a WHERE with `AND`, verify the expression tree

---

## Lesson 8: Query Executor

**Goal:** Wire the parser to the storage layer — execute parsed SQL statements.

**Concepts:**
- The executor receives an AST node and performs the corresponding storage operations
- CREATE TABLE → call Catalog.CreateTable
- INSERT → serialize the row, insert into the table's heap file
- SELECT → scan the heap file, filter rows against the WHERE clause, return matches
- DELETE → scan, find matching rows, delete them by RowId
- WHERE evaluation: compare deserialized column values against the expression

**Files to create:**
- `TobbeSQL/Execution/QueryExecutor.cs`
- `TobbeSQL/Execution/QueryResult.cs` — holds the result: rows + column names (for SELECT) or affected row count (for INSERT/DELETE)
- `TobbeSQL/Execution/ExpressionEvaluator.cs` — evaluates a WHERE expression against a row

**QueryExecutor should have:**
- Constructor that takes the `Catalog`
- `Execute(statement)` → returns `QueryResult`
- Private methods: `ExecuteCreateTable(...)`, `ExecuteInsert(...)`, `ExecuteSelect(...)`, `ExecuteDelete(...)`, `ExecuteCreateIndex(...)`

**Tests (QueryExecutorTests.cs) — file: `TobbeSQL.Tests/Execution/QueryExecutorTests.cs`:**
- `CreateTable_ThenSelectStar_ReturnsEmptyResult` — create a table, select from it, verify 0 rows but correct column names
- `InsertAndSelect_ReturnsInsertedRow` — insert a row, select *, verify the row is returned with correct values
- `InsertMultiple_SelectAll_ReturnsAllRows` — insert 3 rows, select *, verify count is 3
- `SelectWithWhere_ReturnsOnlyMatchingRows` — insert 3 rows, select with WHERE on one, verify only 1 returned
- `Delete_RemovesMatchingRows` — insert 2 rows, delete one by WHERE, select *, verify only 1 remains
- `SelectSpecificColumns_ReturnsOnlyThoseColumns` — insert a row, select only one column, verify result has just that column

---

## Lesson 9: CLI (Command-Line Interface)

**Goal:** Accept a SQL command as a command-line argument, execute it, and print the result.

**Concepts:**
- The binary takes a single SQL statement as a CLI argument: `dotnet run -- "SELECT * FROM users"`
- Parse the argument, execute it, print the result to stdout
- Format SELECT results as a simple text table (column headers + rows)
- Print errors nicely to stderr (parse errors, unknown table, etc.)
- Exit code 0 on success, 1 on error
- Special command: `.tables` lists all tables in the catalog

**Files to modify:**
- `TobbeSQL/Program.cs` — the main entry point reads `args[0]` and executes it

**Tests:**
- No unit tests for this lesson — it's a thin CLI layer over the executor
- Test manually by running SQL commands from the terminal

---

## Lesson 10: B-Tree Index

**Goal:** Implement a B-tree stored on pages for fast lookups by indexed columns.

**Concepts:**
- A B-tree is a balanced search tree optimized for disk (each node = one page)
- Two node types: internal nodes (keys + child page pointers) and leaf nodes (keys + RowIds)
- Search: start at root, at each node find which child to follow, repeat until leaf
- Insert: find the right leaf, add the entry; if the leaf is full, split it
- The executor should use the index when a WHERE clause matches an indexed column
- CREATE INDEX builds the tree by scanning existing rows and inserting each one

**Files to create:**
- `TobbeSQL/Storage/BTree.cs` — the B-tree implementation
- `TobbeSQL/Storage/BTreeNode.cs` — represents one node (one page)

**BTree should have:**
- Constructor that takes a `PageManager` and the root page number
- `Search(object key)` → returns list of RowIds matching the key
- `Insert(object key, RowId rowId)`
- `Delete(object key, RowId rowId)`
- Static `Create(PageManager pm)` → allocates root page, returns new BTree

**BTreeNode should have:**
- Methods to read/write node data from/to a page byte array
- `IsLeaf` property
- Key/pointer/RowId accessors

**Tests (BTreeTests.cs) — file: `TobbeSQL.Tests/Storage/BTreeTests.cs`:**
- `InsertAndSearch_SingleKey_ReturnsCorrectRowId` — insert one key/RowId pair, search for it, verify match
- `InsertMany_SearchEach_AllFound` — insert 20 key/RowId pairs, search for each, verify all found
- `Search_NonExistentKey_ReturnsEmpty` — search for a key that was never inserted, verify empty result
- `InsertEnoughToSplit_StillFindsAllKeys` — insert 200+ entries (forces node splits), verify all are searchable
- `Delete_RemovesKeyFromSearch` — insert a key, delete it, verify search returns empty

---

## Lesson Status Tracker

| Lesson | Topic                | Status      |
|--------|----------------------|-------------|
| 1      | Page Manager         | Done        |
| 2      | Schema & Serializer  | Done        |
| 3      | Slotted Page         | Done        |
| 4      | Heap File            | Done        |
| 5      | Catalog              | Done        |
| 6      | SQL Tokenizer        | Done        |
| 7      | SQL Parser           | Done        |
| 8      | Query Executor       | Done        |
| 9      | CLI                  | Done        |
| 10     | B-Tree Index         | Not started |
