using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JPCenter.Modules.Library.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LibraryDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "documents",
                schema: "library",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    subtitle = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    category = table.Column<string>(type: "text", nullable: false),
                    file_type = table.Column<string>(type: "text", nullable: false),
                    file_key = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    file_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    size = table.Column<long>(type: "bigint", nullable: true),
                    pages = table.Column<int>(type: "integer", nullable: true),
                    duration = table.Column<int>(type: "integer", nullable: true),
                    levels = table.Column<string[]>(type: "text[]", nullable: false),
                    skills = table.Column<string[]>(type: "text[]", nullable: false),
                    tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    publisher = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    year = table.Column<int>(type: "integer", nullable: true),
                    audience = table.Column<string[]>(type: "text[]", nullable: false),
                    branch_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    watermark = table.Column<bool>(type: "boolean", nullable: false),
                    allow_download = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    uploaded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    view_count = table.Column<int>(type: "integer", nullable: false),
                    reader_count = table.Column<int>(type: "integer", nullable: false),
                    avg_minutes = table.Column<decimal>(type: "numeric(8,1)", precision: 8, scale: 1, nullable: false),
                    last_viewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("pk_documents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "document_views",
                schema: "library",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    viewer_type = table.Column<string>(type: "text", nullable: false),
                    viewer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    viewer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    viewer_role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    minutes = table.Column<decimal>(type: "numeric(6,1)", precision: 6, scale: 1, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_views", x => x.id);
                    table.ForeignKey(
                        name: "fk_document_views_documents_document_id",
                        column: x => x.document_id,
                        principalSchema: "library",
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_document_views_document_id_started_at",
                schema: "library",
                table: "document_views",
                columns: new[] { "document_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "ix_document_views_viewer_type_viewer_id",
                schema: "library",
                table: "document_views",
                columns: new[] { "viewer_type", "viewer_id" });

            migrationBuilder.CreateIndex(
                name: "ix_documents_branch_ids",
                schema: "library",
                table: "documents",
                column: "branch_ids")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_documents_levels",
                schema: "library",
                table: "documents",
                column: "levels")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_documents_status_category",
                schema: "library",
                table: "documents",
                columns: new[] { "status", "category" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_views",
                schema: "library");

            migrationBuilder.DropTable(
                name: "documents",
                schema: "library");
        }
    }
}
