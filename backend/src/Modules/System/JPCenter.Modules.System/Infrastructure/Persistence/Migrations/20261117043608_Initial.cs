using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JPCenter.Modules.System.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "system");

            migrationBuilder.CreateTable(
                name: "approval_requests",
                schema: "system",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    detail = table.Column<string>(type: "text", nullable: true),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    requested_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_by_name = table.Column<string>(type: "text", nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    decided_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_by_name = table.Column<string>(type: "text", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_approval_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                schema: "system",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_name = table.Column<string>(type: "text", nullable: false),
                    actor_role = table.Column<string>(type: "text", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    category = table.Column<string>(type: "text", nullable: false),
                    severity = table.Column<string>(type: "text", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    target = table.Column<string>(type: "text", nullable: false),
                    detail = table.Column<string>(type: "text", nullable: true),
                    changes = table.Column<string>(type: "jsonb", nullable: false),
                    ip = table.Column<string>(type: "text", nullable: false),
                    device = table.Column<string>(type: "text", nullable: false),
                    trace_id = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "branches",
                schema: "system",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    address = table.Column<string>(type: "text", nullable: false),
                    phone = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    manager_id = table.Column<Guid>(type: "uuid", nullable: true),
                    open_hours = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    opened_at = table.Column<DateOnly>(type: "date", nullable: false),
                    bank_name = table.Column<string>(type: "text", nullable: true),
                    bank_account = table.Column<string>(type: "text", nullable: true),
                    bank_holder = table.Column<string>(type: "text", nullable: true),
                    invoice_prefix = table.Column<string>(type: "text", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_branches", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "idempotency_keys",
                schema: "system",
                columns: table => new
                {
                    scope = table.Column<string>(type: "text", nullable: false),
                    key = table.Column<string>(type: "text", nullable: false),
                    request_hash = table.Column<string>(type: "text", nullable: false),
                    status_code = table.Column<int>(type: "integer", nullable: true),
                    body = table.Column<string>(type: "text", nullable: true),
                    content_type = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_idempotency_keys", x => new { x.scope, x.key });
                });

            migrationBuilder.CreateTable(
                name: "integrations",
                schema: "system",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    account = table.Column<string>(type: "text", nullable: true),
                    connected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    connected_by = table.Column<Guid>(type: "uuid", nullable: true),
                    last_sync_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    config = table.Column<string>(type: "jsonb", nullable: false),
                    events = table.Column<string>(type: "jsonb", nullable: false),
                    webhook_url = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_integrations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "one_time_tokens",
                schema: "system",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<string>(type: "text", nullable: false),
                    account_kind = table.Column<string>(type: "text", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recipient = table.Column<string>(type: "text", nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    max_attempts = table.Column<int>(type: "integer", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    invalidated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_one_time_tokens", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "portal_accounts",
                schema: "system",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_type = table.Column<string>(type: "text", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    username = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: true),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    must_change_password = table.Column<bool>(type: "boolean", nullable: false),
                    token_version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    failed_login_count = table.Column<int>(type: "integer", nullable: false),
                    locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_portal_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                schema: "system",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    is_super_admin = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "settings",
                schema: "system",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "trash_items",
                schema: "system",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "text", nullable: false),
                    detail = table.Column<string>(type: "text", nullable: true),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: false),
                    purge_after = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trash_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_accounts",
                schema: "system",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    locked_reason = table.Column<string>(type: "text", nullable: true),
                    must_change_password = table.Column<bool>(type: "boolean", nullable: false),
                    token_version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    failed_login_count = table.Column<int>(type: "integer", nullable: false),
                    locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_sessions",
                schema: "system",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_kind = table.Column<string>(type: "text", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    refresh_token_hash = table.Column<string>(type: "text", nullable: false),
                    previous_token_hash = table.Column<string>(type: "text", nullable: true),
                    device = table.Column<string>(type: "text", nullable: false),
                    ip = table.Column<string>(type: "text", nullable: false),
                    remember = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoke_reason = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "integration_logs",
                schema: "system",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    integration_id = table.Column<Guid>(type: "uuid", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ok = table.Column<bool>(type: "boolean", nullable: false),
                    message = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_integration_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_integration_logs_integrations_integration_id",
                        column: x => x.integration_id,
                        principalSchema: "system",
                        principalTable: "integrations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_grants",
                schema: "system",
                columns: table => new
                {
                    permission = table.Column<string>(type: "text", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_grants", x => new { x.role_id, x.permission });
                    table.ForeignKey(
                        name: "fk_role_grants_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "system",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "permission_overrides",
                schema: "system",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission = table.Column<string>(type: "text", nullable: false),
                    effect = table.Column<string>(type: "text", nullable: false),
                    scope = table.Column<string>(type: "text", nullable: true),
                    until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: false),
                    granted_by = table.Column<Guid>(type: "uuid", nullable: false),
                    granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    user_account_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permission_overrides", x => x.id);
                    table.ForeignKey(
                        name: "fk_permission_overrides_user_accounts_user_account_id",
                        column: x => x.user_account_id,
                        principalSchema: "system",
                        principalTable: "user_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_role_assignments",
                schema: "system",
                columns: table => new
                {
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_role_assignments", x => new { x.user_account_id, x.role_id });
                    table.ForeignKey(
                        name: "fk_user_role_assignments_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "system",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_role_assignments_user_accounts_user_account_id",
                        column: x => x.user_account_id,
                        principalSchema: "system",
                        principalTable: "user_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_approval_requests_code",
                schema: "system",
                table: "approval_requests",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_approval_requests_status_requested_at",
                schema: "system",
                table: "approval_requests",
                columns: new[] { "status", "requested_at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_actor_id_at",
                schema: "system",
                table: "audit_logs",
                columns: new[] { "actor_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_at",
                schema: "system",
                table: "audit_logs",
                column: "at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_category_at",
                schema: "system",
                table: "audit_logs",
                columns: new[] { "category", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_branches_code",
                schema: "system",
                table: "branches",
                column: "code",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_idempotency_keys_expires_at",
                schema: "system",
                table: "idempotency_keys",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_integration_logs_integration_id_at",
                schema: "system",
                table: "integration_logs",
                columns: new[] { "integration_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_integrations_key",
                schema: "system",
                table: "integrations",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_one_time_tokens_recipient_purpose_created_at",
                schema: "system",
                table: "one_time_tokens",
                columns: new[] { "recipient", "purpose", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_one_time_tokens_token_hash",
                schema: "system",
                table: "one_time_tokens",
                column: "token_hash");

            migrationBuilder.CreateIndex(
                name: "ix_permission_overrides_user_account_id",
                schema: "system",
                table: "permission_overrides",
                column: "user_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_portal_accounts_email",
                schema: "system",
                table: "portal_accounts",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "ix_portal_accounts_owner_type_owner_id",
                schema: "system",
                table: "portal_accounts",
                columns: new[] { "owner_type", "owner_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_portal_accounts_username",
                schema: "system",
                table: "portal_accounts",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_roles_name",
                schema: "system",
                table: "roles",
                column: "name",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_settings_key",
                schema: "system",
                table: "settings",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_trash_items_kind_entity_id",
                schema: "system",
                table: "trash_items",
                columns: new[] { "kind", "entity_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_trash_items_purge_after",
                schema: "system",
                table: "trash_items",
                column: "purge_after");

            migrationBuilder.CreateIndex(
                name: "ix_user_accounts_email",
                schema: "system",
                table: "user_accounts",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_accounts_employee_id",
                schema: "system",
                table: "user_accounts",
                column: "employee_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_role_assignments_role_id",
                schema: "system",
                table: "user_role_assignments",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_sessions_account_kind_account_id",
                schema: "system",
                table: "user_sessions",
                columns: new[] { "account_kind", "account_id" });

            migrationBuilder.CreateIndex(
                name: "ix_user_sessions_previous_token_hash",
                schema: "system",
                table: "user_sessions",
                column: "previous_token_hash");

            migrationBuilder.CreateIndex(
                name: "ix_user_sessions_refresh_token_hash",
                schema: "system",
                table: "user_sessions",
                column: "refresh_token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "approval_requests",
                schema: "system");

            migrationBuilder.DropTable(
                name: "audit_logs",
                schema: "system");

            migrationBuilder.DropTable(
                name: "branches",
                schema: "system");

            migrationBuilder.DropTable(
                name: "idempotency_keys",
                schema: "system");

            migrationBuilder.DropTable(
                name: "integration_logs",
                schema: "system");

            migrationBuilder.DropTable(
                name: "one_time_tokens",
                schema: "system");

            migrationBuilder.DropTable(
                name: "permission_overrides",
                schema: "system");

            migrationBuilder.DropTable(
                name: "portal_accounts",
                schema: "system");

            migrationBuilder.DropTable(
                name: "role_grants",
                schema: "system");

            migrationBuilder.DropTable(
                name: "settings",
                schema: "system");

            migrationBuilder.DropTable(
                name: "trash_items",
                schema: "system");

            migrationBuilder.DropTable(
                name: "user_role_assignments",
                schema: "system");

            migrationBuilder.DropTable(
                name: "user_sessions",
                schema: "system");

            migrationBuilder.DropTable(
                name: "integrations",
                schema: "system");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "system");

            migrationBuilder.DropTable(
                name: "user_accounts",
                schema: "system");
        }
    }
}
