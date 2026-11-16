using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Payment.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChangeBuyerIdToString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "BuyerId",
                table: "Payments",
                type: "text",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "BuyerId",
                table: "Payments",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}
