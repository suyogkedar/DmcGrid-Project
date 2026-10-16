-- DMCGrid schema. Idempotent: safe to run on every application start.
-- Batches are separated by GO and executed by DbInitializer.

IF OBJECT_ID('dbo.Users') IS NULL
CREATE TABLE dbo.Users
(
    UserId          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
    Email           NVARCHAR(256)  NOT NULL CONSTRAINT UQ_Users_Email UNIQUE,
    PasswordHash    NVARCHAR(200)  NOT NULL,
    FullName        NVARCHAR(150)  NOT NULL,
    Role            NVARCHAR(20)   NOT NULL CONSTRAINT CK_Users_Role CHECK (Role IN ('Agent','DMC')),
    OrgName         NVARCHAR(200)  NOT NULL,
    GstNumber       NVARCHAR(30)   NOT NULL CONSTRAINT DF_Users_Gst DEFAULT (''),
    Phone           NVARCHAR(30)   NOT NULL CONSTRAINT DF_Users_Phone DEFAULT (''),
    Destinations    NVARCHAR(500)  NOT NULL CONSTRAINT DF_Users_Dest DEFAULT (''),
    LeadsRemaining  INT            NOT NULL CONSTRAINT DF_Users_Leads DEFAULT (0),
    WalletBalance   DECIMAL(18,2)  NOT NULL CONSTRAINT DF_Users_Wallet DEFAULT (0),
    QuoteCredits    INT            NOT NULL CONSTRAINT DF_Users_Credits DEFAULT (0),
    IsActive        BIT            NOT NULL CONSTRAINT DF_Users_Active DEFAULT (1),
    CreatedAt       DATETIME2(0)   NOT NULL CONSTRAINT DF_Users_Created DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_Users_Balances CHECK (LeadsRemaining >= 0 AND WalletBalance >= 0 AND QuoteCredits >= 0)
);
GO

IF OBJECT_ID('dbo.Rfqs') IS NULL
CREATE TABLE dbo.Rfqs
(
    RfqId          INT IDENTITY(1048,1) NOT NULL CONSTRAINT PK_Rfqs PRIMARY KEY,
    RfqNo          AS (CONCAT('RFQ-', RfqId)),
    AgentUserId    INT            NOT NULL CONSTRAINT FK_Rfqs_Agent REFERENCES dbo.Users(UserId),
    Destination    NVARCHAR(100)  NOT NULL,
    TravelYear     INT            NOT NULL,
    TravelMonth    NVARCHAR(20)   NOT NULL,
    StartDate      DATE           NULL,
    EndDate        DATE           NULL,
    Nights         INT            NULL,
    Adults         INT            NOT NULL,
    Children       INT            NOT NULL,
    FlightTime     NVARCHAR(40)   NOT NULL,
    Stops          NVARCHAR(40)   NOT NULL,
    Cabin          NVARCHAR(40)   NOT NULL,
    HotelCategory  NVARCHAR(40)   NOT NULL,
    RoomType       NVARCHAR(40)   NOT NULL,
    MealPlan       NVARCHAR(40)   NOT NULL,
    Cities         NVARCHAR(300)  NOT NULL,
    Vehicle        NVARCHAR(40)   NOT NULL,
    FoodPrefs      NVARCHAR(200)  NOT NULL,
    Allergies      NVARCHAR(300)  NOT NULL,
    Budget         NVARCHAR(40)   NOT NULL,
    Priorities     NVARCHAR(1000) NOT NULL,
    NaturalText    NVARCHAR(2000) NOT NULL,
    AiFields       NVARCHAR(300)  NOT NULL,
    Status         NVARCHAR(20)   NOT NULL CONSTRAINT CK_Rfqs_Status CHECK (Status IN ('Draft','Published','Closed')),
    CreatedAt      DATETIME2(0)   NOT NULL CONSTRAINT DF_Rfqs_Created DEFAULT (SYSUTCDATETIME()),
    UpdatedAt      DATETIME2(0)   NOT NULL CONSTRAINT DF_Rfqs_Updated DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_Rfqs_Dates CHECK (EndDate IS NULL OR StartDate IS NULL OR EndDate >= StartDate)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Rfqs_Agent')
    CREATE INDEX IX_Rfqs_Agent ON dbo.Rfqs(AgentUserId, Status);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Rfqs_Status_Destination')
    CREATE INDEX IX_Rfqs_Status_Destination ON dbo.Rfqs(Status, Destination);
GO

IF OBJECT_ID('dbo.Quotes') IS NULL
CREATE TABLE dbo.Quotes
(
    QuoteId      INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Quotes PRIMARY KEY,
    RfqId        INT           NOT NULL CONSTRAINT FK_Quotes_Rfq REFERENCES dbo.Rfqs(RfqId),
    DmcUserId    INT           NOT NULL CONSTRAINT FK_Quotes_Dmc REFERENCES dbo.Users(UserId),
    Status       NVARCHAR(20)  NOT NULL CONSTRAINT CK_Quotes_Status CHECK (Status IN ('Draft','Submitted')),
    SubmittedAt  DATETIME2(0)  NULL,
    CreatedAt    DATETIME2(0)  NOT NULL CONSTRAINT DF_Quotes_Created DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_Quotes_Rfq_Dmc UNIQUE (RfqId, DmcUserId)
);
GO

IF OBJECT_ID('dbo.QuotePackages') IS NULL
CREATE TABLE dbo.QuotePackages
(
    PackageId        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_QuotePackages PRIMARY KEY,
    QuoteId          INT            NOT NULL CONSTRAINT FK_QuotePackages_Quote REFERENCES dbo.Quotes(QuoteId) ON DELETE CASCADE,
    Name             NVARCHAR(150)  NOT NULL,
    NetPrice         DECIMAL(18,2)  NOT NULL,
    Hotel            NVARCHAR(200)  NOT NULL,
    Room             NVARCHAR(100)  NOT NULL,
    Meal             NVARCHAR(100)  NOT NULL,
    Vehicle          NVARCHAR(100)  NOT NULL,
    Food             NVARCHAR(200)  NOT NULL,
    DayCount         INT            NOT NULL,
    VersionNo        INT            NOT NULL CONSTRAINT DF_QuotePackages_Version DEFAULT (1),
    ParentPackageId  INT            NULL,   -- package this one revises; kept by value so history survives deletes
    AfterConnection  BIT            NOT NULL CONSTRAINT DF_QuotePackages_After DEFAULT (0),
    StatusNote       NVARCHAR(300)  NOT NULL CONSTRAINT DF_QuotePackages_Note DEFAULT (''),
    SortOrder        INT            NOT NULL,
    CreatedAt        DATETIME2(0)   NOT NULL CONSTRAINT DF_QuotePackages_Created DEFAULT (SYSUTCDATETIME()),
    UpdatedAt        DATETIME2(0)   NOT NULL CONSTRAINT DF_QuotePackages_Updated DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_QuotePackages_Price CHECK (NetPrice >= 0)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_QuotePackages_Quote')
    CREATE INDEX IX_QuotePackages_Quote ON dbo.QuotePackages(QuoteId, SortOrder);
GO

IF OBJECT_ID('dbo.PackageActivities') IS NULL
CREATE TABLE dbo.PackageActivities
(
    ActivityId   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PackageActivities PRIMARY KEY,
    PackageId    INT            NOT NULL CONSTRAINT FK_PackageActivities_Package REFERENCES dbo.QuotePackages(PackageId) ON DELETE CASCADE,
    DayNo        INT            NOT NULL,
    SortOrder    INT            NOT NULL,
    FromTime     NVARCHAR(10)   NOT NULL,
    ToTime       NVARCHAR(10)   NOT NULL,
    City         NVARCHAR(100)  NOT NULL,
    Description  NVARCHAR(500)  NOT NULL,
    Transfer     NVARCHAR(100)  NOT NULL,
    Included     BIT            NOT NULL,
    Notes        NVARCHAR(300)  NOT NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PackageActivities_Package')
    CREATE INDEX IX_PackageActivities_Package ON dbo.PackageActivities(PackageId, DayNo, SortOrder);
GO

IF OBJECT_ID('dbo.RfqConnections') IS NULL
CREATE TABLE dbo.RfqConnections
(
    ConnectionId  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RfqConnections PRIMARY KEY,
    RfqId         INT           NOT NULL CONSTRAINT FK_RfqConnections_Rfq REFERENCES dbo.Rfqs(RfqId),
    DmcUserId     INT           NOT NULL CONSTRAINT FK_RfqConnections_Dmc REFERENCES dbo.Users(UserId),
    ConnectedAt   DATETIME2(0)  NOT NULL CONSTRAINT DF_RfqConnections_At DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_RfqConnections UNIQUE (RfqId, DmcUserId)
);
GO

IF OBJECT_ID('dbo.WalletTransactions') IS NULL
CREATE TABLE dbo.WalletTransactions
(
    TxnId      INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_WalletTransactions PRIMARY KEY,
    UserId     INT            NOT NULL CONSTRAINT FK_WalletTransactions_User REFERENCES dbo.Users(UserId),
    RfqId      INT            NULL CONSTRAINT FK_WalletTransactions_Rfq REFERENCES dbo.Rfqs(RfqId),
    TxnType    NVARCHAR(30)   NOT NULL,   -- WalletDebit | LeadConsumed | PlanPurchase | QuoteCredit
    Amount     DECIMAL(18,2)  NOT NULL,
    Leads      INT            NOT NULL,
    Note       NVARCHAR(300)  NOT NULL,
    CreatedAt  DATETIME2(0)   NOT NULL CONSTRAINT DF_WalletTransactions_At DEFAULT (SYSUTCDATETIME())
);
GO

IF OBJECT_ID('dbo.Proposals') IS NULL
CREATE TABLE dbo.Proposals
(
    ProposalId      INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Proposals PRIMARY KEY,
    RfqId           INT            NOT NULL CONSTRAINT FK_Proposals_Rfq REFERENCES dbo.Rfqs(RfqId),
    AgentUserId     INT            NOT NULL CONSTRAINT FK_Proposals_Agent REFERENCES dbo.Users(UserId),
    PackageId       INT            NULL CONSTRAINT FK_Proposals_Package REFERENCES dbo.QuotePackages(PackageId) ON DELETE SET NULL,
    SourceLabel     NVARCHAR(200)  NOT NULL,
    NetCost         DECIMAL(18,2)  NOT NULL,
    MarginType      NVARCHAR(20)   NOT NULL CONSTRAINT CK_Proposals_MarginType CHECK (MarginType IN ('Percentage','Fixed')),
    Margin          DECIMAL(18,2)  NOT NULL,
    GstPct          DECIMAL(9,4)   NOT NULL,
    TcsPct          DECIMAL(9,4)   NOT NULL,
    Travellers      INT            NOT NULL,
    Validity        NVARCHAR(50)   NOT NULL,
    BrandName       NVARCHAR(200)  NOT NULL,
    Title           NVARCHAR(200)  NOT NULL,
    GstNumber       NVARCHAR(30)   NOT NULL,
    Contact         NVARCHAR(200)  NOT NULL,
    Inclusions      NVARCHAR(2000) NOT NULL,
    Exclusions      NVARCHAR(2000) NOT NULL,
    TdsPaid         DECIMAL(18,2)  NOT NULL,
    TdsPayable      DECIMAL(18,2)  NOT NULL,
    ApprovalStatus  NVARCHAR(20)   NOT NULL CONSTRAINT CK_Proposals_Approval CHECK (ApprovalStatus IN ('Draft','Pending','Approved')),
    ApprovedBy      INT            NULL,
    ApprovedAt      DATETIME2(0)   NULL,
    CreatedAt       DATETIME2(0)   NOT NULL CONSTRAINT DF_Proposals_Created DEFAULT (SYSUTCDATETIME()),
    UpdatedAt       DATETIME2(0)   NOT NULL CONSTRAINT DF_Proposals_Updated DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_Proposals_Rfq UNIQUE (RfqId)
);
GO

IF OBJECT_ID('dbo.ProposalDays') IS NULL
CREATE TABLE dbo.ProposalDays
(
    ProposalId  INT            NOT NULL CONSTRAINT FK_ProposalDays_Proposal REFERENCES dbo.Proposals(ProposalId) ON DELETE CASCADE,
    DayNo       INT            NOT NULL,
    City        NVARCHAR(100)  NOT NULL,
    CONSTRAINT PK_ProposalDays PRIMARY KEY (ProposalId, DayNo)
);
GO

IF OBJECT_ID('dbo.ProposalItems') IS NULL
CREATE TABLE dbo.ProposalItems
(
    ItemId      INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ProposalItems PRIMARY KEY,
    ProposalId  INT            NOT NULL,
    DayNo       INT            NOT NULL,
    SortOrder   INT            NOT NULL,
    TimeText    NVARCHAR(10)   NOT NULL,
    ItemType    NVARCHAR(30)   NOT NULL,
    Details     NVARCHAR(500)  NOT NULL,
    Reference   NVARCHAR(300)  NOT NULL,
    CONSTRAINT FK_ProposalItems_Day FOREIGN KEY (ProposalId, DayNo) REFERENCES dbo.ProposalDays(ProposalId, DayNo) ON DELETE CASCADE
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProposalItems_Day')
    CREATE INDEX IX_ProposalItems_Day ON dbo.ProposalItems(ProposalId, DayNo, SortOrder);
GO

IF OBJECT_ID('dbo.DealCosts') IS NULL
CREATE TABLE dbo.DealCosts
(
    DealCostId      INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DealCosts PRIMARY KEY,
    ProposalId      INT            NOT NULL CONSTRAINT FK_DealCosts_Proposal REFERENCES dbo.Proposals(ProposalId) ON DELETE CASCADE,
    SortOrder       INT            NOT NULL,
    CostType        NVARCHAR(50)   NOT NULL,
    Description     NVARCHAR(300)  NOT NULL,
    AgentCost       DECIMAL(18,2)  NOT NULL,
    CustomerCharge  DECIMAL(18,2)  NOT NULL
);
GO

IF OBJECT_ID('dbo.AuditLog') IS NULL
CREATE TABLE dbo.AuditLog
(
    AuditId    BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuditLog PRIMARY KEY,
    UserId     INT            NULL,
    Action     NVARCHAR(60)   NOT NULL,
    Entity     NVARCHAR(60)   NOT NULL,
    EntityId   INT            NULL,
    Details    NVARCHAR(1000) NOT NULL,
    CreatedAt  DATETIME2(0)   NOT NULL CONSTRAINT DF_AuditLog_At DEFAULT (SYSUTCDATETIME())
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLog_Entity')
    CREATE INDEX IX_AuditLog_Entity ON dbo.AuditLog(Entity, EntityId, CreatedAt);
GO
