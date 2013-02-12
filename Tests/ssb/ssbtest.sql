CREATE MESSAGE TYPE NGinn_Msg 
VALIDATION = NONE
GO 

CREATE CONTRACT NGinn_Contract 
(NGinn_Msg SENT BY ANY) 
GO 

CREATE QUEUE NG_MQ1
CREATE QUEUE NG_MQ2 
GO 

CREATE SERVICE NG_Service1
ON QUEUE NG_MQ1
(NGinn_Contract) 

CREATE SERVICE NG_Service2 
ON QUEUE NG_MQ2
(NGinn_Contract) 
GO 

DECLARE @h UNIQUEIDENTIFIER 

BEGIN DIALOG CONVERSATION @h 
   FROM SERVICE NG_Service1 
   TO SERVICE NG_Service2 
   ON CONTRACT NGinn_Contract 
   WITH ENCRYPTION=OFF 

--Don't lose this GUID! 
SELECT @h 
GO 

347CF529-A7F2-E011-99D3-824624749296

DECLARE @h UNIQUEIDENTIFIER 
--Insert the GUID from the last section 
SET @h = '347CF529-A7F2-E011-99D3-824624749296'

;SEND ON CONVERSATION @h  
MESSAGE TYPE NGinn_Msg 
('{"Id" : 989233, "Test" : "ala ma kota"}') 
GO 

DECLARE  
    @h UNIQUEIDENTIFIER, 
    @t sysname, 
    @b varbinary(MAX) 

--Note the semicolon..! 
;RECEIVE TOP(1)  
    @h = conversation_handle, 
    @t = message_type_name, 
    @b = message_body 
FROM NG_MQ2 

--Make sure not to lose the handle! 
IF @t = 'Simple_Msg' 
BEGIN 
    SELECT  
        CONVERT(XML, @b), 
        @h 
END 
ELSE 
BEGIN 
    RAISERROR( 
        'I don''t know what to do with this type of message!',  
        16, 1) 
END 
GO 
