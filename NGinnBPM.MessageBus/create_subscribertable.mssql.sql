IF  not exists (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[{0}]') AND type in (N'U'))
BEGIN
create table dbo.[{0}] (
	publisher_endpoint varchar(200) not null,
	subscriber_endpoint varchar(200) not null,
	message_type varchar(300) not null,
	created_date datetime not null,
	expiration_date datetime null,
	CONSTRAINT [PK_{0}] PRIMARY KEY CLUSTERED 
	(
		publisher_endpoint,
		message_type,
		subscriber_endpoint
	) 
)
END
