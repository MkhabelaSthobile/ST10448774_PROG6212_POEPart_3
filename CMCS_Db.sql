-- Creating adatabase
USE master
If EXISTS (SELECT * FROM sys.databases WHERE name = 'CMCS_Db')
DROP DATABASE CMCS_Db;
CREATE DATABASE CMCS_Db;
USE CMCS_Db;

-- Create tables

CREATE TABLE Lecturers (
    LecturerID	INT IDENTITY(1,1) PRIMARY KEY,
    FullName	VARCHAR(100) NOT NULL,
    Email		NVARCHAR(100) NOT NULL,
    Password	VARCHAR(255) NOT NULL,
    ModuleName	VARCHAR(100) NOT NULL,
    HourlyRate	DECIMAL(10,2) NOT NULL
);

CREATE TABLE ProgrammeCoordinators (
    CoordinatorID	INT IDENTITY(1,1) PRIMARY KEY,
    Name			VARCHAR(100) NOT NULL,
    Email			NVARCHAR(100) NOT NULL,
	Password		VARCHAR(255) NOT NULL
);

CREATE TABLE AcademicManagers (
    ManagerID	INT IDENTITY(1,1) PRIMARY KEY,
    FullName	NVARCHAR(100) NOT NULL,
    Email		NVARCHAR(100) NOT NULL,
	Password	VARCHAR(255) NOT NULL
);

CREATE TABLE Claims (
    ClaimID			INT IDENTITY(1,1) PRIMARY KEY,
    LecturerID		INT NOT NULL REFERENCES Lecturers(LecturerID),
    ModuleName		NVARCHAR(100) NULL,
    Month			NVARCHAR(50) NOT NULL,
    HoursWorked		INT NOT NULL,
    HourlyRate		DECIMAL(10,2) NOT NULL,
    TotalAmount		DECIMAL(10,2) NOT NULL,
    Status			NVARCHAR(100) NOT NULL DEFAULT 'Submitted',
    SubmissionDate	DATETIME2 NOT NULL DEFAULT GETDATE(),
    SupportingDocument NVARCHAR(255) NULL,
    RejectionReason NVARCHAR(500) NULL
);

-- NEW: Users table for authentication
CREATE TABLE Users (
    UserID			INT IDENTITY(1,1) PRIMARY KEY,
    FullName		NVARCHAR(100) NOT NULL,
    Email			NVARCHAR(100) NOT NULL UNIQUE,
    Password		NVARCHAR(255) NOT NULL,
    Role			NVARCHAR(50) NOT NULL, -- Lecturer, Coordinator, Manager, HR
    LecturerID		INT NULL,
    CoordinatorID	INT NULL,
    ManagerID		INT NULL,
    CreatedDate		DATETIME2 NOT NULL DEFAULT GETDATE(),
    IsActive		BIT NOT NULL DEFAULT 1
);


--Inserting

INSERT INTO Lecturers (FullName, Email, Password, ModuleName, HourlyRate)
VALUES 
('Dr. John Smith', 'john.smith@university.ac.za', 'JS5523', 'Computer Science', 350.00),
('Prof. Sarah Wilson', 'sarah.wilson@university.ac.za', 'WilSarah101', 'Mathematics', 320.00),
('Dr. Michael Brown', 'michael.brown@university.ac.za', 'MBrown_599', 'Physics', 380.00),
('Dr. Emily Davis', 'emily.davis@university.ac.za', 'EDPW156', 'Business Management', 340.00);


INSERT INTO ProgrammeCoordinators (Name, Email, Password)
VALUES	('David Johnson', 'david.johnson@university.ac.za', 'PCDavid@001!'),
		('Lara-Jean Peckham', 'lisa.anderson@university.ac.za', 'PCLaraJean@002!'),
		('Amy Schnitzel', 'amy.schnitzel@university.ac.za', 'PCAmy@003!');


INSERT INTO AcademicManagers (FullName, Email, Password)
VALUES	('James Carbonara', 'james.carb@university.ac.za', 'JCAM@6633'),
		('Prof. Sarah Martinez', 'sarah.martinezzz@university.ac.za', 'SMAM@1254'),
		('Liam Payne', 'liam.payyyne@university.ac.za', 'LPAM@5821');


INSERT INTO Claims (LecturerID, ModuleName, Month, HoursWorked, HourlyRate, TotalAmount, Status, SubmissionDate)
VALUES	
(1, 'Computer Science', 'January 2024', 40, 350.00, 14000.00, 'Approved by Manager', DATEADD(DAY, -30, GETDATE())),
(1, 'Computer Science', 'February 2024', 35, 350.00, 12250.00, 'Approved by Coordinator', DATEADD(DAY, -15, GETDATE())),
(2, 'Mathematics', 'January 2024', 38, 320.00, 12160.00, 'Submitted', DATEADD(DAY, -10, GETDATE())),
(2, 'Mathematics', 'February 2024', 42, 320.00, 13440.00, 'Rejected by Coordinator', DATEADD(DAY, -5, GETDATE())),
(3, 'Physics', 'January 2024', 36, 380.00, 13680.00, 'Submitted', DATEADD(DAY, -3, GETDATE())),
(4, 'Business Management', 'February 2024', 39, 340.00, 13260.00, 'Approved by Manager', DATEADD(DAY, -1, GETDATE()));

-- HR Admin Accounts
INSERT INTO Users (FullName, Email, Password, Role, LecturerID, CoordinatorID, ManagerID)
VALUES	('HR Admin 1', 'hr1@cmcs.ac.za', 'ADMIN001!', 'HR', NULL, NULL, NULL),
		('HR Admin 2', 'hr2@cmcs.ac.za', 'ADMIN002!', 'HR', NULL, NULL, NULL);


-- Update rejection reason for rejected claim
UPDATE Claims 
SET RejectionReason = 'Hours exceed contract limit' 
WHERE ClaimID = 4;


SELECT * FROM Lecturers
SELECT * FROM ProgrammeCoordinators
SELECT * FROM AcademicManagers
SELECT * FROM Claims
SELECT * FROM Users

