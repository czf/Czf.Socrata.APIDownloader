﻿USE [master]
GO

/****** Object:  Database [Czf.Soda.Demo]    Script Date: 11/28/2021 5:59:09 PM ******/
CREATE DATABASE [Czf.Soda.Demo]
 CONTAINMENT = NONE
 ON  PRIMARY 
( NAME = N'Czf.Soda.Demo', FILENAME = N'C:\Program Files\Microsoft SQL Server\MSSQL15.MSSQLSERVER\MSSQL\DATA\Czf.Soda.Demo.mdf' , SIZE = 8192KB , MAXSIZE = UNLIMITED, FILEGROWTH = 65536KB )
 LOG ON 
( NAME = N'Czf.Soda.Demo_log', FILENAME = N'C:\Program Files\Microsoft SQL Server\MSSQL15.MSSQLSERVER\MSSQL\DATA\Czf.Soda.Demo_log.ldf' , SIZE = 8192KB , MAXSIZE = 2048GB , FILEGROWTH = 65536KB )
 WITH CATALOG_COLLATION = DATABASE_DEFAULT
GO

IF (1 = FULLTEXTSERVICEPROPERTY('IsFullTextInstalled'))
begin
EXEC [Czf.Soda.Demo].[dbo].[sp_fulltext_database] @action = 'enable'
end
GO

ALTER DATABASE [Czf.Soda.Demo] SET ANSI_NULL_DEFAULT OFF 
GO

ALTER DATABASE [Czf.Soda.Demo] SET ANSI_NULLS OFF 
GO

ALTER DATABASE [Czf.Soda.Demo] SET ANSI_PADDING OFF 
GO

ALTER DATABASE [Czf.Soda.Demo] SET ANSI_WARNINGS OFF 
GO

ALTER DATABASE [Czf.Soda.Demo] SET ARITHABORT OFF 
GO

ALTER DATABASE [Czf.Soda.Demo] SET AUTO_CLOSE OFF 
GO

ALTER DATABASE [Czf.Soda.Demo] SET AUTO_SHRINK OFF 
GO

ALTER DATABASE [Czf.Soda.Demo] SET AUTO_UPDATE_STATISTICS ON 
GO

ALTER DATABASE [Czf.Soda.Demo] SET CURSOR_CLOSE_ON_COMMIT OFF 
GO

ALTER DATABASE [Czf.Soda.Demo] SET CURSOR_DEFAULT  GLOBAL 
GO

ALTER DATABASE [Czf.Soda.Demo] SET CONCAT_NULL_YIELDS_NULL OFF 
GO

ALTER DATABASE [Czf.Soda.Demo] SET NUMERIC_ROUNDABORT OFF 
GO

ALTER DATABASE [Czf.Soda.Demo] SET QUOTED_IDENTIFIER OFF 
GO

ALTER DATABASE [Czf.Soda.Demo] SET RECURSIVE_TRIGGERS OFF 
GO

ALTER DATABASE [Czf.Soda.Demo] SET  DISABLE_BROKER 
GO

ALTER DATABASE [Czf.Soda.Demo] SET AUTO_UPDATE_STATISTICS_ASYNC OFF 
GO

ALTER DATABASE [Czf.Soda.Demo] SET DATE_CORRELATION_OPTIMIZATION OFF 
GO

ALTER DATABASE [Czf.Soda.Demo] SET TRUSTWORTHY OFF 
GO

ALTER DATABASE [Czf.Soda.Demo] SET ALLOW_SNAPSHOT_ISOLATION OFF 
GO

ALTER DATABASE [Czf.Soda.Demo] SET PARAMETERIZATION SIMPLE 
GO

ALTER DATABASE [Czf.Soda.Demo] SET READ_COMMITTED_SNAPSHOT OFF 
GO

ALTER DATABASE [Czf.Soda.Demo] SET HONOR_BROKER_PRIORITY OFF 
GO

ALTER DATABASE [Czf.Soda.Demo] SET RECOVERY SIMPLE 
GO

ALTER DATABASE [Czf.Soda.Demo] SET  MULTI_USER 
GO

ALTER DATABASE [Czf.Soda.Demo] SET PAGE_VERIFY CHECKSUM  
GO

ALTER DATABASE [Czf.Soda.Demo] SET DB_CHAINING OFF 
GO

ALTER DATABASE [Czf.Soda.Demo] SET FILESTREAM( NON_TRANSACTED_ACCESS = OFF ) 
GO

ALTER DATABASE [Czf.Soda.Demo] SET TARGET_RECOVERY_TIME = 60 SECONDS 
GO

ALTER DATABASE [Czf.Soda.Demo] SET DELAYED_DURABILITY = DISABLED 
GO

ALTER DATABASE [Czf.Soda.Demo] SET ACCELERATED_DATABASE_RECOVERY = OFF  
GO

ALTER DATABASE [Czf.Soda.Demo] SET QUERY_STORE = OFF
GO

ALTER DATABASE [Czf.Soda.Demo] SET  READ_WRITE 
GO

