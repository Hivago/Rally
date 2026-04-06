using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RallyAPI.Users.Infrastructure.Persistence.Migrations
{
    public partial class AddMissingRestaurantColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // These columns should have been added by AddRestaurantOwnerAndOutletSupport
            // but are missing in production. Using raw SQL with IF NOT EXISTS to be safe.
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='users' AND table_name='restaurants' AND column_name='cuisine_types') THEN
                        ALTER TABLE users.restaurants ADD COLUMN cuisine_types jsonb NOT NULL DEFAULT '[]'::jsonb;
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='users' AND table_name='restaurants' AND column_name='is_pure_veg') THEN
                        ALTER TABLE users.restaurants ADD COLUMN is_pure_veg boolean NOT NULL DEFAULT false;
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='users' AND table_name='restaurants' AND column_name='is_vegan_friendly') THEN
                        ALTER TABLE users.restaurants ADD COLUMN is_vegan_friendly boolean NOT NULL DEFAULT false;
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='users' AND table_name='restaurants' AND column_name='has_jain_options') THEN
                        ALTER TABLE users.restaurants ADD COLUMN has_jain_options boolean NOT NULL DEFAULT false;
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='users' AND table_name='restaurants' AND column_name='min_order_amount') THEN
                        ALTER TABLE users.restaurants ADD COLUMN min_order_amount numeric(10,2) NOT NULL DEFAULT 0;
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='users' AND table_name='restaurants' AND column_name='fssai_number') THEN
                        ALTER TABLE users.restaurants ADD COLUMN fssai_number varchar(20) NULL;
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='users' AND table_name='restaurants' AND column_name='owner_id') THEN
                        ALTER TABLE users.restaurants ADD COLUMN owner_id uuid NULL;
                    END IF;
                END $$;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op — don't drop columns that may have data
        }
    }
}
