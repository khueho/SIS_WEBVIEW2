-- ===================================================================
-- SCRIPT KHỞI TẠO DATABASE VÀ BẢNG CHO SIS_WEBVIEW2
-- Database: SisPatientDb
-- Table   : PatientAccessInfo
-- ===================================================================

-- 1. Tạo Database nếu chưa tồn tại
IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = N'SisPatientDb')
BEGIN
    CREATE DATABASE [SisPatientDb];
    PRINT N'✅ Đã tạo Database SisPatientDb thành công.';
END
GO

USE [SisPatientDb];
GO

-- 2. Tạo Bảng PatientAccessInfo nếu chưa tồn tại
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[PatientAccessInfo]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[PatientAccessInfo] (
        [PatientId]         NVARCHAR(50)  NOT NULL,
        [PatientName]       NVARCHAR(255) NULL,
        [Username]          NVARCHAR(100) NULL,
        [TemporaryPassword] NVARCHAR(255) NULL,
        [PortalUrl]         NVARCHAR(500) NULL DEFAULT 'https://portal.sisvietnam.vn',
        [SavedAt]           DATETIME      NULL DEFAULT GETDATE(),
        [EmailSentStatus]   NVARCHAR(50)  NULL DEFAULT 'PENDING',
        [EmailSentAt]       DATETIME      NULL,
        [EmailError]        NVARCHAR(MAX) NULL,
        
        CONSTRAINT [PK_PatientAccessInfo] PRIMARY KEY CLUSTERED ([PatientId] ASC)
    );
    PRINT N'✅ Đã tạo Bảng PatientAccessInfo thành công.';
END
ELSE
BEGIN
    PRINT N'ℹ️ Bảng PatientAccessInfo đã tồn tại. Đang kiểm tra bổ sung cột Email...';
    
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PatientAccessInfo]') AND name = 'EmailSentStatus')
    BEGIN
        ALTER TABLE [dbo].[PatientAccessInfo] ADD [EmailSentStatus] NVARCHAR(50) NULL DEFAULT 'PENDING';
        PRINT N'  + Đã thêm cột EmailSentStatus.';
    END

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PatientAccessInfo]') AND name = 'EmailSentAt')
    BEGIN
        ALTER TABLE [dbo].[PatientAccessInfo] ADD [EmailSentAt] DATETIME NULL;
        PRINT N'  + Đã thêm cột EmailSentAt.';
    END

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PatientAccessInfo]') AND name = 'EmailError')
    BEGIN
        ALTER TABLE [dbo].[PatientAccessInfo] ADD [EmailError] NVARCHAR(MAX) NULL;
        PRINT N'  + Đã thêm cột EmailError.';
    END
END
GO

-- 3. Tạo Bảng DiagnosticImagingPatients (Lưu danh sách lấy từ API) nếu chưa tồn tại
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DiagnosticImagingPatients]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[DiagnosticImagingPatients] (
        [PatientCode]    NVARCHAR(50)  NOT NULL,
        [FullName]       NVARCHAR(255) NULL,
        [Address]        NVARCHAR(500) NULL,
        [PhoneNumber]    NVARCHAR(50)  NULL,
        [Email]          NVARCHAR(255) NULL,
        [SentDate]       NVARCHAR(50)  NULL,
        [ProcessStatus]  NVARCHAR(50)  NULL DEFAULT 'PENDING',
        [FetchedAt]      DATETIME      NULL DEFAULT GETDATE(),
        [ProcessedAt]    DATETIME      NULL,
        
        CONSTRAINT [PK_DiagnosticImagingPatients] PRIMARY KEY CLUSTERED ([PatientCode] ASC)
    );
    CREATE INDEX [IX_DiagnosticImagingPatients_Status] ON [dbo].[DiagnosticImagingPatients]([ProcessStatus]);
    PRINT N'✅ Đã tạo Bảng DiagnosticImagingPatients thành công.';
END
ELSE
BEGIN
    PRINT N'ℹ️ Bảng DiagnosticImagingPatients đã tồn tại.';
END
GO

