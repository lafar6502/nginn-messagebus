IF  not exists (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[{0}]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[{0}](
	[id] [varchar](100) NOT NULL,
	[data] [text] not null,
	[last_modified] datetime not null
PRIMARY KEY CLUSTERED 
(
	[id] ASC
) WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON)) 

END
