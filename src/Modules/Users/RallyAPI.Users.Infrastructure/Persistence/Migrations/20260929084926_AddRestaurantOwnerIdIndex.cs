using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RallyAPI.Users.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantOwnerIdIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The model snapshot has carried a by-convention FK index on Restaurant.OwnerId
            // (default name IX_restaurants_owner_id) since the FK relationship was added, but
            // no prior migration actually created it — same class of migration-history drift
            // as 20260422000000_EnsureRestaurantOwnersTable. Handle both possible starting
            // states: rename it if some environment does have the default-named index, or
            // create it fresh (idx_restaurants_owner_id) if none exists yet.
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM pg_indexes
                        WHERE schemaname = 'users' AND indexname = 'IX_restaurants_owner_id'
                    ) THEN
                        ALTER INDEX users.""IX_restaurants_owner_id"" RENAME TO idx_restaurants_owner_id;
                    ELSIF NOT EXISTS (
                        SELECT 1 FROM pg_indexes
                        WHERE schemaname = 'users' AND indexname = 'idx_restaurants_owner_id'
                    ) THEN
                        CREATE INDEX idx_restaurants_owner_id ON users.restaurants (owner_id);
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS users.idx_restaurants_owner_id;
            ");
        }
    }
}
