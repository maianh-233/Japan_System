using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JPCenter.Modules.Academic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AcademicQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "questions",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    level = table.Column<string>(type: "text", nullable: false),
                    skill = table.Column<string>(type: "text", nullable: false),
                    format = table.Column<string>(type: "text", nullable: false),
                    difficulty = table.Column<string>(type: "text", nullable: false),
                    stem = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    passage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    audio_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    audio_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    audio_duration_sec = table.Column<int>(type: "integer", nullable: true),
                    transcript = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    options = table.Column<List<string>>(type: "text[]", nullable: false),
                    answer = table.Column<int>(type: "integer", nullable: false),
                    explanation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    curriculum_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lesson_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    review_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    used_count = table.Column<int>(type: "integer", nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    correct_count = table.Column<int>(type: "integer", nullable: false),
                    picks = table.Column<List<int>>(type: "integer[]", nullable: false),
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
                    table.PrimaryKey("pk_questions", x => x.id);
                    table.ForeignKey(
                        name: "fk_questions_curricula_curriculum_id",
                        column: x => x.curriculum_id,
                        principalSchema: "academic",
                        principalTable: "curricula",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_questions_author_id",
                schema: "academic",
                table: "questions",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_questions_code",
                schema: "academic",
                table: "questions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_questions_curriculum_id",
                schema: "academic",
                table: "questions",
                column: "curriculum_id");

            migrationBuilder.CreateIndex(
                name: "ix_questions_lesson_ids",
                schema: "academic",
                table: "questions",
                column: "lesson_ids")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_questions_status_level_skill",
                schema: "academic",
                table: "questions",
                columns: new[] { "status", "level", "skill" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "questions",
                schema: "academic");
        }
    }
}
