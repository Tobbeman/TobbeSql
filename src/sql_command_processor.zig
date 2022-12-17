const std = @import("std");

const SqlStatmentType = enum {
    CreateTable
};

const SqlStatement = struct {
    type: SqlStatmentType,
    table: []const u8
};

const SqlProcessingResult = struct {
    statement: ?SqlStatement,
    errorMessage: ?[]const u8
};

pub fn processCommand(command: []const u8) SqlProcessingResult {
    var tokens = std.mem.tokenize(u8, command, " ");
    if (std.mem.eql(u8, tokens.next().?, "create")) {
        if (std.mem.eql(u8, tokens.next().?, "table")) {
            return SqlProcessingResult {
                .statement = SqlStatement {
                    .type = SqlStatmentType.CreateTable,
                    .table = tokens.next().?
                },
                .errorMessage = null
            };
        } else {
        return SqlProcessingResult {
                .statement = null,
                .errorMessage = "Did not understand create command"
            };
        }
    } else {
        return SqlProcessingResult {
            .statement = null,
            .errorMessage = "Shit is fucked"
        };
    }
}


test "process create table" {    
    const command = processCommand("create table tobbe");
    try std.testing.expect(command.statement.?.type == SqlStatmentType.CreateTable);
    try std.testing.expect(std.mem.eql(u8, command.statement.?.table, "tobbe"));
}