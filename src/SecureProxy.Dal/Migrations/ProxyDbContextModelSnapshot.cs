using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SecureProxy.Dal.Data;

#nullable disable

namespace SecureProxy.Dal.Migrations
{
    [DbContext(typeof(ProxyDbContext))]
    partial class ProxyDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.10")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("SecureProxy.Dal.Domain.ConfigurationChangeLog", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<long>("Id"));

                    b.Property<string>("Action")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<DateTimeOffset>("ChangedAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("ChangedBy")
                        .HasMaxLength(200)
                        .HasColumnType("nvarchar(200)");

                    b.Property<string>("EntityId")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<string>("EntityType")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<string>("Summary")
                        .HasMaxLength(1000)
                        .HasColumnType("nvarchar(1000)");

                    b.HasKey("Id");

                    b.ToTable("ConfigurationChangeLogs", (string)null);
                });

            modelBuilder.Entity("SecureProxy.Dal.Domain.Destination", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<string>("AllowedPorts")
                        .HasMaxLength(200)
                        .HasColumnType("nvarchar(200)");

                    b.Property<string>("AuthProfileRef")
                        .HasMaxLength(200)
                        .HasColumnType("nvarchar(200)");

                    b.Property<string>("BaseUrl")
                        .IsRequired()
                        .HasMaxLength(2000)
                        .HasColumnType("nvarchar(2000)");

                    b.Property<DateTimeOffset>("CreatedAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<bool>("IsActive")
                        .HasColumnType("bit");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("nvarchar(200)");

                    b.Property<byte[]>("RowVersion")
                        .IsConcurrencyToken()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("rowversion");

                    b.Property<int>("TimeoutMs")
                        .HasColumnType("int");

                    b.Property<DateTimeOffset>("UpdatedAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.HasKey("Id");

                    b.HasIndex("Name")
                        .IsUnique();

                    b.ToTable("Destinations", (string)null);
                });

            modelBuilder.Entity("SecureProxy.Dal.Domain.EncryptionProfile", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<string>("Algorithm")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<DateTimeOffset>("CreatedAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("KeyRef")
                        .IsRequired()
                        .HasMaxLength(500)
                        .HasColumnType("nvarchar(500)");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("nvarchar(200)");

                    b.Property<DateTimeOffset>("UpdatedAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.HasKey("Id");

                    b.HasIndex("Name")
                        .IsUnique();

                    b.ToTable("EncryptionProfiles", (string)null);
                });

            modelBuilder.Entity("SecureProxy.Dal.Domain.HeaderRule", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<string>("HeaderName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("nvarchar(200)");

                    b.Property<int>("OperationId")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("OperationId", "HeaderName")
                        .IsUnique();

                    b.ToTable("HeaderRules", (string)null);
                });

            modelBuilder.Entity("SecureProxy.Dal.Domain.Operation", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<DateTimeOffset>("CreatedAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<int>("DestinationId")
                        .HasColumnType("int");

                    b.Property<int?>("EncryptionProfileId")
                        .HasColumnType("int");

                    b.Property<bool>("EncryptionRequired")
                        .HasColumnType("bit");

                    b.Property<string>("HttpMethod")
                        .IsRequired()
                        .HasMaxLength(10)
                        .HasColumnType("nvarchar(10)");

                    b.Property<bool>("IsActive")
                        .HasColumnType("bit");

                    b.Property<long>("MaxRequestBytes")
                        .HasColumnType("bigint");

                    b.Property<long>("MaxResponseBytes")
                        .HasColumnType("bigint");

                    b.Property<string>("OperationKey")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("nvarchar(200)");

                    b.Property<bool>("RejectUnknownParameters")
                        .HasColumnType("bit");

                    b.Property<string>("RelativePathTemplate")
                        .IsRequired()
                        .HasMaxLength(2000)
                        .HasColumnType("nvarchar(2000)");

                    b.Property<byte[]>("RowVersion")
                        .IsConcurrencyToken()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("rowversion");

                    b.Property<DateTimeOffset>("UpdatedAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.HasKey("Id");

                    b.HasIndex("DestinationId");

                    b.HasIndex("EncryptionProfileId");

                    b.HasIndex("OperationKey")
                        .IsUnique();

                    b.ToTable("Operations", (string)null);
                });

            modelBuilder.Entity("SecureProxy.Dal.Domain.ParameterRule", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<int>("DataType")
                        .HasColumnType("int");

                    b.Property<int>("Location")
                        .HasColumnType("int");

                    b.Property<int?>("MaxLength")
                        .HasColumnType("int");

                    b.Property<long?>("MaxValue")
                        .HasColumnType("bigint");

                    b.Property<long?>("MinValue")
                        .HasColumnType("bigint");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("nvarchar(200)");

                    b.Property<int>("OperationId")
                        .HasColumnType("int");

                    b.Property<string>("Pattern")
                        .HasMaxLength(500)
                        .HasColumnType("nvarchar(500)");

                    b.Property<bool>("Required")
                        .HasColumnType("bit");

                    b.HasKey("Id");

                    b.HasIndex("OperationId", "Name")
                        .IsUnique();

                    b.ToTable("ParameterRules", (string)null);
                });

            modelBuilder.Entity("SecureProxy.Dal.Domain.RequestLog", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<long>("Id"));

                    b.Property<DateTimeOffset?>("CompletedAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("CorrelationId")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<int?>("DestinationId")
                        .HasColumnType("int");

                    b.Property<long>("DurationMs")
                        .HasColumnType("bigint");

                    b.Property<bool>("ForwardingOccurred")
                        .HasColumnType("bit");

                    b.Property<string>("OperationKey")
                        .HasMaxLength(200)
                        .HasColumnType("nvarchar(200)");

                    b.Property<int>("Outcome")
                        .HasColumnType("int");

                    b.Property<int>("ProxyStatusCode")
                        .HasColumnType("int");

                    b.Property<string>("ReasonCode")
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<DateTimeOffset>("StartedAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<int?>("UpstreamStatusCode")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("CorrelationId");

                    b.HasIndex("StartedAtUtc");

                    b.ToTable("RequestLogs", (string)null);
                });

            modelBuilder.Entity("SecureProxy.Dal.Domain.HeaderRule", b =>
                {
                    b.HasOne("SecureProxy.Dal.Domain.Operation", "Operation")
                        .WithMany("HeaderRules")
                        .HasForeignKey("OperationId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Operation");
                });

            modelBuilder.Entity("SecureProxy.Dal.Domain.Operation", b =>
                {
                    b.HasOne("SecureProxy.Dal.Domain.Destination", "Destination")
                        .WithMany("Operations")
                        .HasForeignKey("DestinationId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("SecureProxy.Dal.Domain.EncryptionProfile", "EncryptionProfile")
                        .WithMany()
                        .HasForeignKey("EncryptionProfileId")
                        .OnDelete(DeleteBehavior.Restrict);

                    b.Navigation("Destination");

                    b.Navigation("EncryptionProfile");
                });

            modelBuilder.Entity("SecureProxy.Dal.Domain.ParameterRule", b =>
                {
                    b.HasOne("SecureProxy.Dal.Domain.Operation", "Operation")
                        .WithMany("ParameterRules")
                        .HasForeignKey("OperationId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Operation");
                });

            modelBuilder.Entity("SecureProxy.Dal.Domain.Destination", b =>
                {
                    b.Navigation("Operations");
                });

            modelBuilder.Entity("SecureProxy.Dal.Domain.Operation", b =>
                {
                    b.Navigation("HeaderRules");

                    b.Navigation("ParameterRules");
                });
#pragma warning restore 612, 618
        }
    }
}
