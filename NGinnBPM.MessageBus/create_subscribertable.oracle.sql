create table {0} (
	publisher_endpoint varchar2(200) not null,
	subscriber_endpoint varchar2(200) not null,
	message_type varchar2(300) not null,
	created_date date not null,
	expiration_date date null,
	CONSTRAINT PK_{0} PRIMARY KEY 
	(
		publisher_endpoint,
		message_type,
		subscriber_endpoint
	) 
)

