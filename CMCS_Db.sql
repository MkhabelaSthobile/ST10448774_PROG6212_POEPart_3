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
    Email			NVARCHAR(100) NOT NULL
);

CREATE TABLE AcademicManagers (
    ManagerID	INT IDENTITY(1,1) PRIMARY KEY,
    FullName	NVARCHAR(100) NOT NULL,
    Email		NVARCHAR(100) NOT NULL
);

CREATE TABLE Claims (
    ClaimID			INT IDENTITY(1,1) PRIMARY KEY,
    LecturerID		INT NOT NULL REFERENCES Lecturers(LecturerID),
    Month			NVARCHAR(50) NOT NULL,
    HoursWorked		INT NOT NULL,
    HourlyRate		DECIMAL(10,2) NOT NULL,
    TotalAmount		DECIMAL(10,2) NOT NULL,
    Status			NVARCHAR(100) NOT NULL DEFAULT 'Pending',
    SubmissionDate	DATETIME2 NOT NULL DEFAULT GETDATE(),
    SupportingDocument NVARCHAR(255) NULL,
    RejectionReason NVARCHAR(500) NULL,
);



--Inserting

INSERT INTO Lecturers (FullName, Email, Password, ModuleName, HourlyRate)
VALUES 
('Dr. John Smith', 'john.smith@university.ac.za', 'JS5523', 'Computer Science', 350.00),
('Prof. Sarah Wilson', 'sarah.wilson@university.ac.za', 'WilSarah101', 'Mathematics', 320.00),
('Dr. Michael Brown', 'michael.brown@university.ac.za', 'MBrown_599', 'Physics', 380.00),
('Dr. Emily Davis', 'emily.davis@university.ac.za', 'EDPW156', 'Business Management', 340.00);


INSERT INTO ProgrammeCoordinators (Name, Email)
VALUES	('David Johnson', 'david.johnson@university.ac.za'),
		('Lara-Jean Peckham', 'lisa.anderson@university.ac.za'),
		('Amy Schnitzel', 'amy.schnitzel@university.ac.za');


INSERT INTO AcademicManagers (FullName, Email)
VALUES	('James Carbonara', 'james.carb@university.ac.za'),
		('Prof. Sarah Martinez', 'sarah.martinezzz@university.ac.za'),
		('Liam Payne', 'liam.payyyne@university.ac.za');


INSERT INTO Claims (LecturerID, Month, HoursWorked, HourlyRate, TotalAmount, Status, SubmissionDate)
VALUES	(1, 'January 2024', 40, 350.00, 14000.00, 'Approved by Manager', GETDATE()-30),
		(1, 'February 2024', 35, 350.00, 12250.00, 'Approved by Coordinator', GETDATE()-15),
		(2, 'January 2024', 38, 320.00, 12160.00, 'Submitted', GETDATE()-10),
		(2, 'February 2024', 42, 320.00, 13440.00, 'Rejected by Coordinator: Hours exceed contract limit', GETDATE()-5),
		(3, 'January 2024', 36, 380.00, 13680.00, 'Pending', GETDATE()-3),
		(4, 'February 2024', 39, 340.00, 13260.00, 'Approved by Manager', GETDATE()-1);

SELECT * FROM Lecturers
SELECT * FROM ProgrammeCoordinators
SELECT * FROM AcademicManagers
SELECT * FROM Claims

DROP TABLE Claims
DROP TABLE Lecturers