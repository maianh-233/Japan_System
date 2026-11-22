using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JPCenter.Modules.Academic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AcademicLearningContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "assignments",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    lesson_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    due_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    max_score = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    quiz = table.Column<string>(type: "jsonb", nullable: true),
                    attachments = table.Column<string>(type: "jsonb", nullable: false),
                    created_by_teacher_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_assignments", x => x.id);
                    table.ForeignKey(
                        name: "fk_assignments_classes_class_id",
                        column: x => x.class_id,
                        principalSchema: "academic",
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "class_announcements",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    content = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    audience = table.Column<string[]>(type: "text[]", nullable: false),
                    channels = table.Column<string[]>(type: "text[]", nullable: false),
                    debtors_only = table.Column<bool>(type: "boolean", nullable: false),
                    student_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    recipients = table.Column<int>(type: "integer", nullable: false),
                    read_count = table.Column<int>(type: "integer", nullable: false),
                    read_by = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    sent_by = table.Column<Guid>(type: "uuid", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    scheduled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_class_announcements", x => x.id);
                    table.ForeignKey(
                        name: "fk_class_announcements_classes_class_id",
                        column: x => x.class_id,
                        principalSchema: "academic",
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "class_exams",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exam_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    mode = table.Column<string>(type: "text", nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    pass_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    shuffle = table.Column<bool>(type: "boolean", nullable: false),
                    sections = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    results_published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_class_exams", x => x.id);
                    table.ForeignKey(
                        name: "fk_class_exams_classes_class_id",
                        column: x => x.class_id,
                        principalSchema: "academic",
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "flash_decks",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    level = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    lesson_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lesson_label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("pk_flash_decks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "teacher_reviews",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    teacher_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period = table.Column<string>(type: "text", nullable: false),
                    overall = table.Column<int>(type: "integer", nullable: false),
                    method = table.Column<int>(type: "integer", nullable: false),
                    clarity = table.Column<int>(type: "integer", nullable: false),
                    enthusiasm = table.Column<int>(type: "integer", nullable: false),
                    punctuality = table.Column<int>(type: "integer", nullable: false),
                    comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    anonymous = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_teacher_reviews", x => x.id);
                    table.ForeignKey(
                        name: "fk_teacher_reviews_classes_class_id",
                        column: x => x.class_id,
                        principalSchema: "academic",
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "submissions",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    files = table.Column<string>(type: "jsonb", nullable: false),
                    content = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    quiz_correct = table.Column<int>(type: "integer", nullable: true),
                    quiz_total = table.Column<int>(type: "integer", nullable: true),
                    quiz_attempts = table.Column<int>(type: "integer", nullable: false),
                    quiz_duration_minutes = table.Column<int>(type: "integer", nullable: true),
                    score = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    graded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    graded_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_submissions", x => x.id);
                    table.ForeignKey(
                        name: "fk_submissions_assignments_assignment_id",
                        column: x => x.assignment_id,
                        principalSchema: "academic",
                        principalTable: "assignments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "exam_attempts",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_exam_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deadline_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    questions = table.Column<string>(type: "jsonb", nullable: false),
                    answers = table.Column<string>(type: "jsonb", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    auto_submitted = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exam_attempts", x => x.id);
                    table.ForeignKey(
                        name: "fk_exam_attempts_class_exams_class_exam_id",
                        column: x => x.class_exam_id,
                        principalSchema: "academic",
                        principalTable: "class_exams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "exam_results",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exam_id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_exam_id = table.Column<Guid>(type: "uuid", nullable: true),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    sections = table.Column<string>(type: "jsonb", nullable: false),
                    total_score = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    max_score = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    duration_minutes = table.Column<int>(type: "integer", nullable: true),
                    comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exam_results", x => x.id);
                    table.ForeignKey(
                        name: "fk_exam_results_class_exams_class_exam_id",
                        column: x => x.class_exam_id,
                        principalSchema: "academic",
                        principalTable: "class_exams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_exam_results_classes_class_id",
                        column: x => x.class_id,
                        principalSchema: "academic",
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "card_progress",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deck_id = table.Column<Guid>(type: "uuid", nullable: false),
                    card_id = table.Column<Guid>(type: "uuid", nullable: false),
                    box = table.Column<int>(type: "integer", nullable: false),
                    due = table.Column<DateOnly>(type: "date", nullable: false),
                    reviews = table.Column<int>(type: "integer", nullable: false),
                    lapses = table.Column<int>(type: "integer", nullable: false),
                    last_reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_card_progress", x => x.id);
                    table.ForeignKey(
                        name: "fk_card_progress_flash_decks_deck_id",
                        column: x => x.deck_id,
                        principalSchema: "academic",
                        principalTable: "flash_decks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "flash_cards",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    front = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    reading = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    meaning = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    example = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    order = table.Column<int>(type: "integer", nullable: false),
                    deck_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_flash_cards", x => x.id);
                    table.ForeignKey(
                        name: "fk_flash_cards_flash_decks_deck_id",
                        column: x => x.deck_id,
                        principalSchema: "academic",
                        principalTable: "flash_decks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_assignments_class_id_due_date",
                schema: "academic",
                table: "assignments",
                columns: new[] { "class_id", "due_date" });

            migrationBuilder.CreateIndex(
                name: "ix_card_progress_deck_id",
                schema: "academic",
                table: "card_progress",
                column: "deck_id");

            migrationBuilder.CreateIndex(
                name: "ix_card_progress_student_id_card_id",
                schema: "academic",
                table: "card_progress",
                columns: new[] { "student_id", "card_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_card_progress_student_id_due",
                schema: "academic",
                table: "card_progress",
                columns: new[] { "student_id", "due" });

            migrationBuilder.CreateIndex(
                name: "ix_class_announcements_class_id_sent_at",
                schema: "academic",
                table: "class_announcements",
                columns: new[] { "class_id", "sent_at" });

            migrationBuilder.CreateIndex(
                name: "ix_class_announcements_scheduled_at",
                schema: "academic",
                table: "class_announcements",
                column: "scheduled_at",
                filter: "sent_at IS NULL AND cancelled_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_class_exams_class_id_exam_id",
                schema: "academic",
                table: "class_exams",
                columns: new[] { "class_id", "exam_id" },
                unique: true,
                filter: "status <> 'cancelled'");

            migrationBuilder.CreateIndex(
                name: "ix_class_exams_status_starts_at",
                schema: "academic",
                table: "class_exams",
                columns: new[] { "status", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "ix_exam_attempts_class_exam_id_student_id",
                schema: "academic",
                table: "exam_attempts",
                columns: new[] { "class_exam_id", "student_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_exam_attempts_deadline_at",
                schema: "academic",
                table: "exam_attempts",
                column: "deadline_at",
                filter: "submitted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_exam_results_class_exam_id",
                schema: "academic",
                table: "exam_results",
                column: "class_exam_id");

            migrationBuilder.CreateIndex(
                name: "ix_exam_results_class_id_exam_id_student_id",
                schema: "academic",
                table: "exam_results",
                columns: new[] { "class_id", "exam_id", "student_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_exam_results_student_id",
                schema: "academic",
                table: "exam_results",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_flash_cards_deck_id_order",
                schema: "academic",
                table: "flash_cards",
                columns: new[] { "deck_id", "order" });

            migrationBuilder.CreateIndex(
                name: "ix_flash_decks_lesson_id",
                schema: "academic",
                table: "flash_decks",
                column: "lesson_id");

            migrationBuilder.CreateIndex(
                name: "ix_flash_decks_level",
                schema: "academic",
                table: "flash_decks",
                column: "level");

            migrationBuilder.CreateIndex(
                name: "ix_submissions_assignment_id_student_id",
                schema: "academic",
                table: "submissions",
                columns: new[] { "assignment_id", "student_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_submissions_student_id",
                schema: "academic",
                table: "submissions",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_teacher_reviews_class_id_teacher_id_student_id_period",
                schema: "academic",
                table: "teacher_reviews",
                columns: new[] { "class_id", "teacher_id", "student_id", "period" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_teacher_reviews_teacher_id",
                schema: "academic",
                table: "teacher_reviews",
                column: "teacher_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "card_progress",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "class_announcements",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "exam_attempts",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "exam_results",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "flash_cards",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "submissions",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "teacher_reviews",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "class_exams",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "flash_decks",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "assignments",
                schema: "academic");
        }
    }
}
