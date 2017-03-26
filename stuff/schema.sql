drop table if exists IncomingMail;
drop table if exists Mail;
drop table if exists MailBox;
drop table if exists ExpiredMail;
drop table if exists ExpiredMailBox;

create table IncomingMail
(
    Id serial not null,
    ReceivedOn timestamp(0) not null,
    Recipient varchar(100) not null,
    Sender varchar(255) not null,
    ContentSize int not null,
    ContentId uuid not null,
    constraint PK_IncomingMail primary key (Id)
);


create table MailBox
(
    Id serial not null,
    Token uuid not null,
    Address varchar(126) not null,
    ExpiresOn timestamp(0) not null,
    constraint PK_MailBox primary key (Id),
    constraint UQ_MailBox__Address unique (Address)
);

create table Mail
(
    Id serial not null,
    IdMailBox integer not null,
    ReceivedOn timestamp(0) not null,
    Size integer,
    constraint PK_Mail primary key (Id),
    constraint FK_Mail__IdMailBox foreign key (IdMailBox) references MailBox(Id)
);

create table ExpiredMailBox
(
    Id integer not null,
    Token uuid not null,
    Address varchar(126) not null,
    ExpiresOn timestamp(0) not null,
    constraint PK_ExpiredMailBox primary key (Id)
);

create table ExpiredMail
(
    Id integer not null,
    IdMailBox integer not null,
    ReceivedOn timestamp(0) not null,
    Size integer,
    constraint PK_ExpiredMail primary key (Id),
    constraint FK_ExpiredMail__IdMailBox foreign key (IdMailBox) references ExpiredMailBox(Id)
);

