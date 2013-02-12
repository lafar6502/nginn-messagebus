IF  not exists (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[{0}]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[{0}](
	[seq_id] [varchar](100) NOT NULL,
	[cur_num] [int] NULL,
	[last_seen_num] [int] NOT NULL,
	[last_seen_date] [datetime] NOT NULL,
	[seq_len] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[seq_id] ASC
) WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON)) 

END
