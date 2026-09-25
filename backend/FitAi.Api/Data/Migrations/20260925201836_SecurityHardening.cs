using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FitAi.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SecurityHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeclinedAt",
                table: "EmailInvites",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CodeChallenge",
                table: "AuthCodes",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeclinedAt",
                table: "EmailInvites");

            migrationBuilder.DropColumn(
                name: "CodeChallenge",
                table: "AuthCodes");
        }
    }
}
