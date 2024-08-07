﻿// <auto-generated />
using System;
using DebtBot.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DebtBot.DB.Migrations
{
    [DbContext(typeof(DebtContext))]
    partial class DebtContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.0")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("DebtBot.DB.Entities.Bill", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid>("CreatorId")
                        .HasColumnType("uuid");

                    b.Property<string>("CurrencyCode")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime>("Date")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("PaymentCurrencyCode")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<byte>("Status")
                        .HasColumnType("smallint");

                    b.Property<decimal>("TotalWithTips")
                        .HasColumnType("decimal(10, 4)");

                    b.HasKey("Id");

                    b.HasIndex("CreatorId");

                    b.ToTable("Bills");
                });

            modelBuilder.Entity("DebtBot.DB.Entities.BillLine", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid>("BillId")
                        .HasColumnType("uuid");

                    b.Property<string>("ItemDescription")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<decimal>("Subtotal")
                        .HasColumnType("decimal(10, 4)");

                    b.HasKey("Id");

                    b.HasIndex("BillId");

                    b.ToTable("BillLines");
                });

            modelBuilder.Entity("DebtBot.DB.Entities.BillLineParticipant", b =>
                {
                    b.Property<Guid>("BillLineId")
                        .HasColumnType("uuid");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.Property<decimal>("Part")
                        .HasColumnType("decimal(10, 4)");

                    b.HasKey("BillLineId", "UserId");

                    b.HasIndex("UserId");

                    b.ToTable("BillLineParticipants");
                });

            modelBuilder.Entity("DebtBot.DB.Entities.BillParticipant", b =>
                {
                    b.Property<Guid>("BillId")
                        .HasColumnType("uuid");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.HasKey("BillId", "UserId");

                    b.HasIndex("UserId");

                    b.ToTable("BillParticipants");
                });

            modelBuilder.Entity("DebtBot.DB.Entities.BillPayment", b =>
                {
                    b.Property<Guid>("BillId")
                        .HasColumnType("uuid");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.Property<decimal>("Amount")
                        .HasColumnType("decimal(10, 4)");

                    b.HasKey("BillId", "UserId");

                    b.HasIndex("UserId");

                    b.ToTable("BillPayments");
                });

            modelBuilder.Entity("DebtBot.DB.Entities.Currency", b =>
                {
                    b.Property<string>("CurrencyCode")
                        .HasMaxLength(3)
                        .HasColumnType("character varying(3)");

                    b.Property<char>("Character")
                        .HasColumnType("character(1)");

                    b.Property<string>("FullName")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("CurrencyCode");

                    b.ToTable("Currencies");
                });

            modelBuilder.Entity("DebtBot.DB.Entities.Debt", b =>
                {
                    b.Property<Guid>("CreditorUserId")
                        .HasColumnType("uuid");

                    b.Property<Guid>("DebtorUserId")
                        .HasColumnType("uuid");

                    b.Property<string>("CurrencyCode")
                        .HasMaxLength(3)
                        .HasColumnType("character varying(3)");

                    b.Property<decimal>("Amount")
                        .HasColumnType("decimal(10, 4)");

                    b.HasKey("CreditorUserId", "DebtorUserId", "CurrencyCode");

                    b.ToTable((string)null);

                    b.ToView("debts", (string)null);
                });

            modelBuilder.Entity("DebtBot.DB.Entities.LedgerRecord", b =>
                {
                    b.Property<Guid>("CreditorUserId")
                        .HasColumnType("uuid");

                    b.Property<Guid>("DebtorUserId")
                        .HasColumnType("uuid");

                    b.Property<Guid>("BillId")
                        .HasColumnType("uuid");

                    b.Property<decimal>("Amount")
                        .HasColumnType("decimal(10, 4)");

                    b.Property<string>("CurrencyCode")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<bool>("IsCanceled")
                        .HasColumnType("boolean");

                    b.HasKey("CreditorUserId", "DebtorUserId", "BillId");

                    b.HasIndex("BillId");

                    b.HasIndex("DebtorUserId");

                    b.ToTable("LedgerRecords");
                });

            modelBuilder.Entity("DebtBot.DB.Entities.NotificationSubscription", b =>
                {
                    b.Property<Guid>("SubscriberId")
                        .HasColumnType("uuid");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.Property<bool>("IsConfirmed")
                        .HasColumnType("boolean");

                    b.HasKey("SubscriberId", "UserId");

                    b.HasIndex("UserId");

                    b.ToTable("NotificationSubscriptions");
                });

            modelBuilder.Entity("DebtBot.DB.Entities.Spending", b =>
                {
                    b.Property<Guid>("BillId")
                        .HasColumnType("uuid");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.Property<decimal>("Amount")
                        .HasColumnType("decimal(10, 4)");

                    b.Property<string>("CurrencyCode")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime>("Date")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<bool>("IsCanceled")
                        .HasColumnType("boolean");

                    b.Property<decimal>("PaymentAmount")
                        .HasColumnType("decimal(10, 4)");

                    b.Property<string>("PaymentCurrencyCode")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<decimal>("Portion")
                        .HasColumnType("decimal(10, 4)");

                    b.HasKey("BillId", "UserId");

                    b.HasIndex("UserId");

                    b.ToTable("Spendings");
                });

            modelBuilder.Entity("DebtBot.DB.Entities.User", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("DisplayName")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Email")
                        .HasColumnType("text");

                    b.Property<byte[]>("ModifiedAt")
                        .IsConcurrencyToken()
                        .IsRequired()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("bytea");

                    b.Property<string>("Phone")
                        .HasColumnType("text");

                    b.Property<byte>("Role")
                        .HasColumnType("smallint");

                    b.Property<bool>("TelegramBotEnabled")
                        .HasColumnType("boolean");

                    b.Property<long?>("TelegramId")
                        .HasColumnType("bigint");

                    b.Property<string>("TelegramUserName")
                        .HasColumnType("text");

                    b.Property<long>("Version")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.ToTable("Users");
                });

            modelBuilder.Entity("DebtBot.DB.Entities.UserContactLink", b =>
                {
                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.Property<Guid>("ContactUserId")
                        .HasColumnType("uuid");

                    b.Property<string>("DisplayName")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("UserId", "ContactUserId");

                    b.HasIndex("ContactUserId");

                    b.ToTable("UserContactsLinks");
                });

            modelBuilder.Entity("DebtBot.DB.Entities.Bill", b =>
                {
                    b.HasOne("DebtBot.DB.Entities.User", "Creator")
                        .WithMany()
                        .HasForeignKey("CreatorId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Creator");
                });

            modelBuilder.Entity("DebtBot.DB.Entities.BillLine", b =>
                {
                    b.HasOne("DebtBot.DB.Entities.Bill", "Bill")
                        .WithMany("Lines")
                        .HasForeignKey("BillId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Bill");
                });

            modelBuilder.Entity("DebtBot.DB.Entities.BillLineParticipant", b =>
                {
                    b.HasOne("DebtBot.DB.Entities.BillLine", "BillLine")
                        .WithMany("Participants")
                        .HasForeignKey("BillLineId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("DebtBot.DB.Entities.User", "User")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("BillLine");

                    b.Navigation("User");
                });

            modelBuilder.Entity("DebtBot.DB.Entities.BillParticipant", b =>
                {
                    b.HasOne("DebtBot.DB.Entities.Bill", "Bill")
                        .WithMany("BillParticipants")
                        .HasForeignKey("BillId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("DebtBot.DB.Entities.User", "User")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Bill");

                    b.Navigation("User");
                });

            modelBuilder.Entity("DebtBot.DB.Entities.BillPayment", b =>
                {
                    b.HasOne("DebtBot.DB.Entities.Bill", "Bill")
                        .WithMany("Payments")
                        .HasForeignKey("BillId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("DebtBot.DB.Entities.User", "User")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Bill");

                    b.Navigation("User");
                });

            modelBuilder.Entity("DebtBot.DB.Entities.Debt", b =>
                {
                    b.HasOne("DebtBot.DB.Entities.User", "CreditorUser")
                        .WithMany("Debts")
                        .HasForeignKey("CreditorUserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("DebtBot.DB.Entities.UserContactLink", "DebtorUser")
                        .WithMany()
                        .HasForeignKey("CreditorUserId", "DebtorUserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("CreditorUser");

                    b.Navigation("DebtorUser");
                });

            modelBuilder.Entity("DebtBot.DB.Entities.LedgerRecord", b =>
                {
                    b.HasOne("DebtBot.DB.Entities.Bill", "Bill")
                        .WithMany("LedgerRecords")
                        .HasForeignKey("BillId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("DebtBot.DB.Entities.User", "CreditorUser")
                        .WithMany("CreditorLedgerRecords")
                        .HasForeignKey("CreditorUserId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("DebtBot.DB.Entities.User", "DebtorUser")
                        .WithMany("DebtorLedgerRecords")
                        .HasForeignKey("DebtorUserId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("Bill");

                    b.Navigation("CreditorUser");

                    b.Navigation("DebtorUser");
                });

            modelBuilder.Entity("DebtBot.DB.Entities.NotificationSubscription", b =>
                {
                    b.HasOne("DebtBot.DB.Entities.User", "Subscriber")
                        .WithMany()
                        .HasForeignKey("SubscriberId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("DebtBot.DB.Entities.User", "User")
                        .WithMany("Subscriptions")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Subscriber");

                    b.Navigation("User");
                });

            modelBuilder.Entity("DebtBot.DB.Entities.Spending", b =>
                {
                    b.HasOne("DebtBot.DB.Entities.Bill", "Bill")
                        .WithMany("Spendings")
                        .HasForeignKey("BillId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("DebtBot.DB.Entities.User", "User")
                        .WithMany("Spendings")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("Bill");

                    b.Navigation("User");
                });

            modelBuilder.Entity("DebtBot.DB.Entities.UserContactLink", b =>
                {
                    b.HasOne("DebtBot.DB.Entities.User", "ContactUser")
                        .WithMany()
                        .HasForeignKey("ContactUserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("DebtBot.DB.Entities.User", "User")
                        .WithMany("UserContacts")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("ContactUser");

                    b.Navigation("User");
                });

            modelBuilder.Entity("DebtBot.DB.Entities.Bill", b =>
                {
                    b.Navigation("BillParticipants");

                    b.Navigation("LedgerRecords");

                    b.Navigation("Lines");

                    b.Navigation("Payments");

                    b.Navigation("Spendings");
                });

            modelBuilder.Entity("DebtBot.DB.Entities.BillLine", b =>
                {
                    b.Navigation("Participants");
                });

            modelBuilder.Entity("DebtBot.DB.Entities.User", b =>
                {
                    b.Navigation("CreditorLedgerRecords");

                    b.Navigation("DebtorLedgerRecords");

                    b.Navigation("Debts");

                    b.Navigation("Spendings");

                    b.Navigation("Subscriptions");

                    b.Navigation("UserContacts");
                });
#pragma warning restore 612, 618
        }
    }
}
