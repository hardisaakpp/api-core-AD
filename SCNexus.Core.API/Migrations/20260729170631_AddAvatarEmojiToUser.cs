using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SCNexus.Core.API.Migrations
{
    /// <inheritdoc />
    public partial class AddAvatarEmojiToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS(SELECT 1 FROM sys.columns 
                              WHERE Name = N'avatar_emoji'
                              AND Object_ID = Object_ID(N'asp_net_users'))
                BEGIN
                    ALTER TABLE asp_net_users ADD avatar_emoji nvarchar(max) NULL;
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "avatar_emoji",
                table: "asp_net_users");
        }
    }
}
