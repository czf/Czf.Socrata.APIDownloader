USE [Czf.Soda.Demo]
GO

/****** Object:  Table [dbo].[EarthquakeDataset]    Script Date: 11/28/2021 6:00:16 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[EarthquakeDataset](
	[Source] [nvarchar](255) NOT NULL,
	[EarthquakeId] [nvarchar](50) NOT NULL,
	[Version] [nvarchar](10) NULL,
	[Magnitude] [decimal](8, 2) NOT NULL,
	[Depth] [decimal](10, 3) NOT NULL,
	[NumberOfStations] [int] NULL,
	[Region] [nvarchar](255) NOT NULL,
	[Location] [geography] NULL,
 CONSTRAINT [PK_EarthquakeDataset] PRIMARY KEY CLUSTERED 
(
	[EarthquakeId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO


