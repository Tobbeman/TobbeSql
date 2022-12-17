const std = @import("std");
const sql_command_processor = @import("sql_command_processor.zig");

fn readLine(reader: anytype, buffer: []u8) !?[]const u8 {
    var line = (try reader.readUntilDelimiterOrEof(
        buffer,
        '\n',
    )) orelse return null;
    
    // trim annoying windows-only carriage return character
    if (@import("builtin").os.tag == .windows) {
        return std.mem.trimRight(u8, line, "\r");
    } else {
        return line;
    }
}

fn doMetaCommand(command: []const u8, writer: anytype) !void {
    if (std.mem.eql(u8, command, ".exit")) {
        std.os.exit(0);
    } else {
        try writer.print("Unknown command\n", .{});
    }
}

pub fn main() !void {
    const reader = std.io.getStdIn().reader();
    const writer = std.io.getStdOut().writer();
    var buf: [256]u8 = undefined;

    while (true) {
        try writer.print("tsql> ", .{});
        var opt_result = readLine(reader, &buf) catch {
            try writer.print("error\n", .{});
            return;
        };
        if (opt_result) |input| {
            if (input[0] == '.') {
                // This function can exit the program
                try doMetaCommand(input, writer);
            } else {
                var result = sql_command_processor.processCommand(input);

                if (result.statement) |command| {
                    try writer.print("statement: {}\n", .{command});
                } else {
                    try writer.print("error: {s}", .{result.errorMessage.?});
                }
            }
        }
    }

    // var opt_result = nextLine(stdin, &buf) catch {
    //     try stdout.print("error\n", .{});
    //     return;
    // };
    // if (opt_result) |input| {
    //     try stdout.print("{s}\n", .{input});
    // }
    

    // const opt_result = stdin.readUntilDelimiterOrEof(&buf, '\n') catch {
    //     try stdout.print("error\n", .{});
    //     return;
    // };
    // if (opt_result) |input| {
    //     try stdout.print("input {s}\n", .{input});
    // } else {
    //      try stdout.print("nothing\n", .{});
    // }


    // while(true) {
    //     try stdout.print("A text please: ", .{});
    //     if (try stdin.readUntilDelimiterOrEof(buf[0..], '\n')) |user_input| {
    //         try stdout.print("{s}\n", .{user_input});
    //         if (std.mem.eql(u8, user_input, ".exit\r")) {
    //             std.os.exit(0);
    //         }
    //     } else {
    //         try stdout.print("sda", .{});
    //     }
    // }



    // while (true) {
    //     try stdout.print("tsql> ", .{});
    //     if (try stdin.readUntilDelimiterOrEof(&buf, '\n')) |input| {
    //         try stdout.print("{s}", .{input});
    //         if (std.mem.eql(u8, input, ".exit")) {
    //             std.os.exit(0);
    //         }
    //     } else {
    //         try stdout.print("wrong", .{});
    //     }
    // }
}