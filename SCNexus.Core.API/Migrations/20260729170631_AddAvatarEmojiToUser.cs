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
            // Column already exists in this DB, added by an untracked migration
            // (AddAvatarFieldsToUser, applied outside source control). No-op to avoid a duplicate-column error.
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
