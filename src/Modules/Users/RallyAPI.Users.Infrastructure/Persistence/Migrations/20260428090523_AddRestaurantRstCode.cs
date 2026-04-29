using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RallyAPI.Users.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantRstCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add the column nullable so existing rows are not blocked.
            migrationBuilder.AddColumn<string>(
                name: "rst_code",
                schema: "users",
                table: "restaurants",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            // 2. Postgres sequence for atomic generation of new codes.
            migrationBuilder.Sql(@"
                CREATE SEQUENCE IF NOT EXISTS users.restaurant_rst_code_seq
                    AS bigint
                    START WITH 1
                    INCREMENT BY 1
                    NO MAXVALUE
                    NO CYCLE;
            ");

            // 3. Backfill existing rows in created_at order, then advance the sequence
            //    so future rows do not collide with the backfilled values.
            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    rec RECORD;
                    seq bigint := 0;
                BEGIN
                    FOR rec IN
                        SELECT id
                        FROM users.restaurants
                        WHERE rst_code IS NULL
                        ORDER BY created_at, id
                    LOOP
                        seq := seq + 1;
                        UPDATE users.restaurants
                        SET rst_code = 'RST' || LPAD(seq::text, 3, '0')
                        WHERE id = rec.id;
                    END LOOP;

                    IF seq > 0 THEN
                        PERFORM setval('users.restaurant_rst_code_seq', seq);
                    END IF;
                END $$;
            ");

            // 4. Unique index, filtered to non-null so any future legacy null rows do not collide.
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS idx_restaurants_rst_code
                    ON users.restaurants (rst_code)
                    WHERE rst_code IS NOT NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS users.idx_restaurants_rst_code;");
            migrationBuilder.Sql(@"DROP SEQUENCE IF EXISTS users.restaurant_rst_code_seq;");

            migrationBuilder.DropColumn(
                name: "rst_code",
                schema: "users",
                table: "restaurants");
        }
    }
}
