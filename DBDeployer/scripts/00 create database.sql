IF NOT EXISTS(select 1 from sysdatabases where name = '{0}' )
BEGIN
CREATE DATABASE [{0}]
END