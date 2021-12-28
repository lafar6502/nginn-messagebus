IF  not exists (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[{0}]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[{0}](
	[id] [bigint] IDENTITY(1,1) NOT NULL,
	[from_endpoint] [varchar](50) NOT NULL,
	[to_endpoint] [varchar](50) NOT NULL,
	[subqueue] [char](1) NOT NULL,
	[insert_time] [datetime] NOT NULL,
	[last_processed] [datetime] NULL,
	[retry_count] [int] NOT NULL,
	[retry_time] [datetime] NOT NULL,
	[error_info] [text] NULL,
	[correlation_id] [varchar](100) NULL,
	[label] [varchar](100) NULL,
	[msg_text] varchar(max) NULL,
	[msg_headers] varchar(max) null,
	[unique_id] varchar(40) null
 CONSTRAINT [PK_{0}] PRIMARY KEY CLUSTERED 
(
	[id] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) 
) 

CREATE NONCLUSTERED INDEX [IDX_{0}_subqueue_retry_time] ON [dbo].[{0}] 
(
	[subqueue] ASC,
	[retry_time] ASC
)
INCLUDE(id)
WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) 

END

