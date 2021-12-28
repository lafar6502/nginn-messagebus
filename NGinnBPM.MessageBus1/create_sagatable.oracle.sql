CREATE TABLE {0} (
	id varchar(100) NOT NULL primary key,
	data NCLOB not null,
	version varchar(20) not null,
	created_date date not null,
	last_updated date not null
)


