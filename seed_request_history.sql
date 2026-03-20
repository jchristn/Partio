-- Seed script: 500 request_history entries scattered over the past year.
-- Run with: sqlite3 partio.db < seed_request_history.sql
--
-- Mix of endpoints:  ~40% /v1.0/process, ~20% /v1.0/process/batch,
--                    ~15% /v1.0/explorer/embedding, ~15% /v1.0/explorer/completion,
--                    ~10% other (health, enumerate, etc.)
-- Mix of statuses:   ~80% 200, ~8% 500, ~5% 400, ~4% 404, ~3% 401

INSERT INTO request_history (id, tenant_id, user_id, credential_id, requestor_ip, http_method, http_url, request_body_length, response_body_length, http_status, response_time_ms, object_key, created_utc, completed_utc) VALUES
('req_seed_00001_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.1','POST','/v1.0/process',512,2048,200,45,NULL,'2025-03-25T08:12:33.0000000Z','2025-03-25T08:12:33.0450000Z'),
('req_seed_00002_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.2','POST','/v1.0/process',480,1920,200,38,NULL,'2025-03-27T14:45:10.0000000Z','2025-03-27T14:45:10.0380000Z'),
('req_seed_00003_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.1','POST','/v1.0/explorer/embedding',126,1553,500,36,NULL,'2025-03-29T03:22:05.0000000Z','2025-03-29T03:22:05.0360000Z'),
('req_seed_00004_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.3','POST','/v1.0/process/batch',4096,16384,200,210,NULL,'2025-04-02T11:05:44.0000000Z','2025-04-02T11:05:44.2100000Z'),
('req_seed_00005_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.1','POST','/v1.0/process',600,2400,200,52,NULL,'2025-04-05T19:33:21.0000000Z','2025-04-05T19:33:21.0520000Z'),
('req_seed_00006_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.2','POST','/v1.0/explorer/completion',242,1779,200,18,NULL,'2025-04-08T06:15:58.0000000Z','2025-04-08T06:15:58.0180000Z'),
('req_seed_00007_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.4','GET','/v1.0/health',0,64,200,2,NULL,'2025-04-10T22:48:37.0000000Z','2025-04-10T22:48:37.0020000Z'),
('req_seed_00008_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.1','POST','/v1.0/process',550,2200,200,47,NULL,'2025-04-13T15:30:14.0000000Z','2025-04-13T15:30:14.0470000Z'),
('req_seed_00009_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.3','POST','/v1.0/process',490,1960,400,12,NULL,'2025-04-16T09:02:51.0000000Z','2025-04-16T09:02:51.0120000Z'),
('req_seed_00010_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.2','POST','/v1.0/process/batch',3800,15200,200,195,NULL,'2025-04-19T01:45:28.0000000Z','2025-04-19T01:45:28.1950000Z'),
('req_seed_00011_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.1','POST','/v1.0/explorer/embedding',130,1600,200,42,NULL,'2025-04-21T18:28:05.0000000Z','2025-04-21T18:28:05.0420000Z'),
('req_seed_00012_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.5','POST','/v1.0/process',520,2080,200,44,NULL,'2025-04-24T12:10:42.0000000Z','2025-04-24T12:10:42.0440000Z'),
('req_seed_00013_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.1','POST','/v1.0/explorer/completion',250,1800,200,22,NULL,'2025-04-27T05:53:19.0000000Z','2025-04-27T05:53:19.0220000Z'),
('req_seed_00014_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.3','POST','/v1.0/process',475,1900,500,15,NULL,'2025-04-29T23:35:56.0000000Z','2025-04-29T23:35:56.0150000Z'),
('req_seed_00015_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.2','POST','/v1.0/process',560,2240,200,50,NULL,'2025-05-02T17:18:33.0000000Z','2025-05-02T17:18:33.0500000Z'),
('req_seed_00016_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.4','POST','/v1.0/process/batch',4200,16800,200,225,NULL,'2025-05-05T10:01:10.0000000Z','2025-05-05T10:01:10.2250000Z'),
('req_seed_00017_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.1','POST','/v1.0/explorer/embedding',128,1560,500,33,NULL,'2025-05-08T03:43:47.0000000Z','2025-05-08T03:43:47.0330000Z'),
('req_seed_00018_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.5','POST','/v1.0/process',540,2160,200,46,NULL,'2025-05-10T21:26:24.0000000Z','2025-05-10T21:26:24.0460000Z'),
('req_seed_00019_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.2','GET','/v1.0/whoami',0,48,200,3,NULL,'2025-05-13T15:09:01.0000000Z','2025-05-13T15:09:01.0030000Z'),
('req_seed_00020_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.3','POST','/v1.0/process',500,2000,200,41,NULL,'2025-05-16T08:51:38.0000000Z','2025-05-16T08:51:38.0410000Z'),
('req_seed_00021_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.1','POST','/v1.0/explorer/completion',260,1850,200,25,NULL,'2025-05-19T02:34:15.0000000Z','2025-05-19T02:34:15.0250000Z'),
('req_seed_00022_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.4','POST','/v1.0/process',530,2120,200,48,NULL,'2025-05-21T20:16:52.0000000Z','2025-05-21T20:16:52.0480000Z'),
('req_seed_00023_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.2','POST','/v1.0/process/batch',3600,14400,200,185,NULL,'2025-05-24T13:59:29.0000000Z','2025-05-24T13:59:29.1850000Z'),
('req_seed_00024_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.5','POST','/v1.0/process',470,1880,401,8,NULL,'2025-05-27T07:42:06.0000000Z','2025-05-27T07:42:06.0080000Z'),
('req_seed_00025_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.1','POST','/v1.0/explorer/embedding',135,1580,200,39,NULL,'2025-05-30T01:24:43.0000000Z','2025-05-30T01:24:43.0390000Z'),
('req_seed_00026_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.3','POST','/v1.0/process',510,2040,200,43,NULL,'2025-06-01T19:07:20.0000000Z','2025-06-01T19:07:20.0430000Z'),
('req_seed_00027_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.2','POST','/v1.0/process',580,2320,200,51,NULL,'2025-06-04T12:49:57.0000000Z','2025-06-04T12:49:57.0510000Z'),
('req_seed_00028_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.4','POST','/v1.0/explorer/completion',245,1790,404,5,NULL,'2025-06-07T06:32:34.0000000Z','2025-06-07T06:32:34.0050000Z'),
('req_seed_00029_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.1','POST','/v1.0/process/batch',4500,18000,200,240,NULL,'2025-06-10T00:15:11.0000000Z','2025-06-10T00:15:11.2400000Z'),
('req_seed_00030_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.5','POST','/v1.0/process',495,1980,200,40,NULL,'2025-06-12T17:57:48.0000000Z','2025-06-12T17:57:48.0400000Z'),
('req_seed_00031_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.3','POST','/v1.0/process',525,2100,500,20,NULL,'2025-06-15T11:40:25.0000000Z','2025-06-15T11:40:25.0200000Z'),
('req_seed_00032_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.2','POST','/v1.0/explorer/embedding',132,1570,200,37,NULL,'2025-06-18T05:23:02.0000000Z','2025-06-18T05:23:02.0370000Z'),
('req_seed_00033_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.1','POST','/v1.0/process',545,2180,200,49,NULL,'2025-06-20T23:05:39.0000000Z','2025-06-20T23:05:39.0490000Z'),
('req_seed_00034_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.4','POST','/v1.0/process',465,1860,200,38,NULL,'2025-06-23T16:48:16.0000000Z','2025-06-23T16:48:16.0380000Z'),
('req_seed_00035_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.5','POST','/v1.0/explorer/completion',255,1820,200,21,NULL,'2025-06-26T10:30:53.0000000Z','2025-06-26T10:30:53.0210000Z'),
('req_seed_00036_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.2','POST','/v1.0/process/batch',3900,15600,200,200,NULL,'2025-06-29T04:13:30.0000000Z','2025-06-29T04:13:30.2000000Z'),
('req_seed_00037_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.1','POST','/v1.0/process',570,2280,200,50,NULL,'2025-07-01T21:56:07.0000000Z','2025-07-01T21:56:07.0500000Z'),
('req_seed_00038_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.3','POST','/v1.0/process',485,1940,400,10,NULL,'2025-07-04T15:38:44.0000000Z','2025-07-04T15:38:44.0100000Z'),
('req_seed_00039_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.4','POST','/v1.0/explorer/embedding',140,1620,200,41,NULL,'2025-07-07T09:21:21.0000000Z','2025-07-07T09:21:21.0410000Z'),
('req_seed_00040_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.5','POST','/v1.0/process',555,2220,200,47,NULL,'2025-07-10T03:03:58.0000000Z','2025-07-10T03:03:58.0470000Z'),
('req_seed_00041_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.1','GET','/v1.0/health',0,64,200,1,NULL,'2025-07-12T20:46:35.0000000Z','2025-07-12T20:46:35.0010000Z'),
('req_seed_00042_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.2','POST','/v1.0/process',505,2020,200,42,NULL,'2025-07-15T14:29:12.0000000Z','2025-07-15T14:29:12.0420000Z'),
('req_seed_00043_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.3','POST','/v1.0/process/batch',4100,16400,500,180,NULL,'2025-07-18T08:11:49.0000000Z','2025-07-18T08:11:49.1800000Z'),
('req_seed_00044_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.4','POST','/v1.0/explorer/completion',248,1810,200,19,NULL,'2025-07-21T01:54:26.0000000Z','2025-07-21T01:54:26.0190000Z'),
('req_seed_00045_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.1','POST','/v1.0/process',535,2140,200,46,NULL,'2025-07-23T19:37:03.0000000Z','2025-07-23T19:37:03.0460000Z'),
('req_seed_00046_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.5','POST','/v1.0/process',460,1840,200,37,NULL,'2025-07-26T13:19:40.0000000Z','2025-07-26T13:19:40.0370000Z'),
('req_seed_00047_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.2','POST','/v1.0/explorer/embedding',125,1545,200,35,NULL,'2025-07-29T07:02:17.0000000Z','2025-07-29T07:02:17.0350000Z'),
('req_seed_00048_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.3','POST','/v1.0/process',590,2360,200,53,NULL,'2025-08-01T00:44:54.0000000Z','2025-08-01T00:44:54.0530000Z'),
('req_seed_00049_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.1','POST','/v1.0/process/batch',3700,14800,200,190,NULL,'2025-08-03T18:27:31.0000000Z','2025-08-03T18:27:31.1900000Z'),
('req_seed_00050_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.4','POST','/v1.0/process',515,2060,401,9,NULL,'2025-08-06T12:10:08.0000000Z','2025-08-06T12:10:08.0090000Z');

-- Entries 51-100: Mid-2025
INSERT INTO request_history (id, tenant_id, user_id, credential_id, requestor_ip, http_method, http_url, request_body_length, response_body_length, http_status, response_time_ms, object_key, created_utc, completed_utc) VALUES
('req_seed_00051_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.2','POST','/v1.0/explorer/completion',258,1840,200,23,NULL,'2025-08-09T05:52:45.0000000Z','2025-08-09T05:52:45.0230000Z'),
('req_seed_00052_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.5','POST','/v1.0/process',540,2160,200,45,NULL,'2025-08-11T23:35:22.0000000Z','2025-08-11T23:35:22.0450000Z'),
('req_seed_00053_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.1','POST','/v1.0/process',480,1920,500,14,NULL,'2025-08-14T17:17:59.0000000Z','2025-08-14T17:17:59.0140000Z'),
('req_seed_00054_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.3','POST','/v1.0/process',565,2260,200,51,NULL,'2025-08-17T11:00:36.0000000Z','2025-08-17T11:00:36.0510000Z'),
('req_seed_00055_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.2','POST','/v1.0/explorer/embedding',138,1610,200,40,NULL,'2025-08-20T04:43:13.0000000Z','2025-08-20T04:43:13.0400000Z'),
('req_seed_00056_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.4','POST','/v1.0/process/batch',4300,17200,200,230,NULL,'2025-08-22T22:25:50.0000000Z','2025-08-22T22:25:50.2300000Z'),
('req_seed_00057_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.1','POST','/v1.0/process',500,2000,200,42,NULL,'2025-08-25T16:08:27.0000000Z','2025-08-25T16:08:27.0420000Z'),
('req_seed_00058_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.5','POST','/v1.0/process',475,1900,200,39,NULL,'2025-08-28T09:51:04.0000000Z','2025-08-28T09:51:04.0390000Z'),
('req_seed_00059_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.3','POST','/v1.0/explorer/completion',262,1860,200,24,NULL,'2025-08-31T03:33:41.0000000Z','2025-08-31T03:33:41.0240000Z'),
('req_seed_00060_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.2','POST','/v1.0/process',520,2080,404,6,NULL,'2025-09-02T21:16:18.0000000Z','2025-09-02T21:16:18.0060000Z'),
('req_seed_00061_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.1','POST','/v1.0/process',550,2200,200,48,NULL,'2025-09-05T14:58:55.0000000Z','2025-09-05T14:58:55.0480000Z'),
('req_seed_00062_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.4','POST','/v1.0/process/batch',3850,15400,200,198,NULL,'2025-09-08T08:41:32.0000000Z','2025-09-08T08:41:32.1980000Z'),
('req_seed_00063_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.5','POST','/v1.0/explorer/embedding',131,1565,500,30,NULL,'2025-09-11T02:24:09.0000000Z','2025-09-11T02:24:09.0300000Z'),
('req_seed_00064_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.3','POST','/v1.0/process',585,2340,200,52,NULL,'2025-09-13T20:06:46.0000000Z','2025-09-13T20:06:46.0520000Z'),
('req_seed_00065_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.2','POST','/v1.0/process',495,1980,200,41,NULL,'2025-09-16T13:49:23.0000000Z','2025-09-16T13:49:23.0410000Z'),
('req_seed_00066_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.1','GET','/v1.0/health',0,64,200,2,NULL,'2025-09-19T07:32:00.0000000Z','2025-09-19T07:32:00.0020000Z'),
('req_seed_00067_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.4','POST','/v1.0/process',510,2040,200,43,NULL,'2025-09-22T01:14:37.0000000Z','2025-09-22T01:14:37.0430000Z'),
('req_seed_00068_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.5','POST','/v1.0/explorer/completion',240,1770,200,17,NULL,'2025-09-24T18:57:14.0000000Z','2025-09-24T18:57:14.0170000Z'),
('req_seed_00069_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.2','POST','/v1.0/process',530,2120,200,46,NULL,'2025-09-27T12:39:51.0000000Z','2025-09-27T12:39:51.0460000Z'),
('req_seed_00070_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.3','POST','/v1.0/process/batch',4000,16000,200,215,NULL,'2025-09-30T06:22:28.0000000Z','2025-09-30T06:22:28.2150000Z'),
('req_seed_00071_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.1','POST','/v1.0/process',560,2240,400,11,NULL,'2025-10-03T00:05:05.0000000Z','2025-10-03T00:05:05.0110000Z'),
('req_seed_00072_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.4','POST','/v1.0/explorer/embedding',127,1555,200,36,NULL,'2025-10-05T17:47:42.0000000Z','2025-10-05T17:47:42.0360000Z'),
('req_seed_00073_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.5','POST','/v1.0/process',575,2300,200,50,NULL,'2025-10-08T11:30:19.0000000Z','2025-10-08T11:30:19.0500000Z'),
('req_seed_00074_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.2','POST','/v1.0/process',490,1960,200,40,NULL,'2025-10-11T05:12:56.0000000Z','2025-10-11T05:12:56.0400000Z'),
('req_seed_00075_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.3','POST','/v1.0/explorer/completion',252,1830,500,16,NULL,'2025-10-13T22:55:33.0000000Z','2025-10-13T22:55:33.0160000Z'),
('req_seed_00076_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.1','POST','/v1.0/process',545,2180,200,47,NULL,'2025-10-16T16:38:10.0000000Z','2025-10-16T16:38:10.0470000Z'),
('req_seed_00077_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.4','POST','/v1.0/process/batch',3950,15800,200,205,NULL,'2025-10-19T10:20:47.0000000Z','2025-10-19T10:20:47.2050000Z'),
('req_seed_00078_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.5','POST','/v1.0/process',505,2020,200,43,NULL,'2025-10-22T04:03:24.0000000Z','2025-10-22T04:03:24.0430000Z'),
('req_seed_00079_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.2','POST','/v1.0/explorer/embedding',136,1595,200,38,NULL,'2025-10-24T21:46:01.0000000Z','2025-10-24T21:46:01.0380000Z'),
('req_seed_00080_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.3','POST','/v1.0/process',520,2080,200,44,NULL,'2025-10-27T15:28:38.0000000Z','2025-10-27T15:28:38.0440000Z'),
('req_seed_00081_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.1','POST','/v1.0/process',470,1880,200,39,NULL,'2025-10-30T09:11:15.0000000Z','2025-10-30T09:11:15.0390000Z'),
('req_seed_00082_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.4','POST','/v1.0/explorer/completion',265,1870,200,26,NULL,'2025-11-02T02:53:52.0000000Z','2025-11-02T02:53:52.0260000Z'),
('req_seed_00083_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.5','POST','/v1.0/process',555,2220,401,7,NULL,'2025-11-04T20:36:29.0000000Z','2025-11-04T20:36:29.0070000Z'),
('req_seed_00084_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.2','POST','/v1.0/process/batch',4400,17600,200,235,NULL,'2025-11-07T14:19:06.0000000Z','2025-11-07T14:19:06.2350000Z'),
('req_seed_00085_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.3','POST','/v1.0/process',500,2000,200,42,NULL,'2025-11-10T08:01:43.0000000Z','2025-11-10T08:01:43.0420000Z'),
('req_seed_00086_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.1','POST','/v1.0/explorer/embedding',129,1558,200,34,NULL,'2025-11-13T01:44:20.0000000Z','2025-11-13T01:44:20.0340000Z'),
('req_seed_00087_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.4','POST','/v1.0/process',580,2320,200,51,NULL,'2025-11-15T19:26:57.0000000Z','2025-11-15T19:26:57.0510000Z'),
('req_seed_00088_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.5','POST','/v1.0/process',465,1860,500,13,NULL,'2025-11-18T13:09:34.0000000Z','2025-11-18T13:09:34.0130000Z'),
('req_seed_00089_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.2','GET','/v1.0/whoami',0,48,200,3,NULL,'2025-11-21T06:52:11.0000000Z','2025-11-21T06:52:11.0030000Z'),
('req_seed_00090_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.3','POST','/v1.0/process',535,2140,200,46,NULL,'2025-11-24T00:34:48.0000000Z','2025-11-24T00:34:48.0460000Z'),
('req_seed_00091_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.1','POST','/v1.0/explorer/completion',247,1800,200,20,NULL,'2025-11-26T18:17:25.0000000Z','2025-11-26T18:17:25.0200000Z'),
('req_seed_00092_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.4','POST','/v1.0/process/batch',3750,15000,200,192,NULL,'2025-11-29T12:00:02.0000000Z','2025-11-29T12:00:02.1920000Z'),
('req_seed_00093_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.5','POST','/v1.0/process',510,2040,200,43,NULL,'2025-12-02T05:42:39.0000000Z','2025-12-02T05:42:39.0430000Z'),
('req_seed_00094_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.2','POST','/v1.0/process',490,1960,200,40,NULL,'2025-12-04T23:25:16.0000000Z','2025-12-04T23:25:16.0400000Z'),
('req_seed_00095_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.3','POST','/v1.0/explorer/embedding',134,1585,404,4,NULL,'2025-12-07T17:07:53.0000000Z','2025-12-07T17:07:53.0040000Z'),
('req_seed_00096_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.1','POST','/v1.0/process',570,2280,200,49,NULL,'2025-12-10T10:50:30.0000000Z','2025-12-10T10:50:30.0490000Z'),
('req_seed_00097_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.4','POST','/v1.0/process',525,2100,200,45,NULL,'2025-12-13T04:33:07.0000000Z','2025-12-13T04:33:07.0450000Z'),
('req_seed_00098_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.5','POST','/v1.0/explorer/completion',256,1835,200,22,NULL,'2025-12-15T22:15:44.0000000Z','2025-12-15T22:15:44.0220000Z'),
('req_seed_00099_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.2','POST','/v1.0/process/batch',4150,16600,200,220,NULL,'2025-12-18T15:58:21.0000000Z','2025-12-18T15:58:21.2200000Z'),
('req_seed_00100_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u','default','default','default','10.0.0.3','POST','/v1.0/process',545,2180,500,18,NULL,'2025-12-21T09:40:58.0000000Z','2025-12-21T09:40:58.0180000Z');

-- Use a recursive CTE to generate entries 101-500 programmatically.
-- This creates 400 more entries spread from 2025-12-24 through 2026-03-20,
-- cycling through endpoints, statuses, and IPs.

WITH RECURSIVE gen(n) AS (
    SELECT 101
    UNION ALL
    SELECT n + 1 FROM gen WHERE n < 500
)
INSERT INTO request_history (id, tenant_id, user_id, credential_id, requestor_ip, http_method, http_url, request_body_length, response_body_length, http_status, response_time_ms, object_key, created_utc, completed_utc)
SELECT
    'req_seed_' || printf('%05d', n) || '_aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2u',
    'default',
    'default',
    'default',
    '10.0.0.' || ((n % 5) + 1),
    CASE
        WHEN n % 20 = 0 THEN 'GET'
        ELSE 'POST'
    END,
    CASE
        WHEN n % 10 < 4  THEN '/v1.0/process'
        WHEN n % 10 < 6  THEN '/v1.0/process/batch'
        WHEN n % 10 < 8  THEN '/v1.0/explorer/embedding'
        WHEN n % 10 < 9  THEN '/v1.0/explorer/completion'
        WHEN n % 20 = 0  THEN '/v1.0/health'
        ELSE '/v1.0/tenants/enumerate'
    END,
    CASE WHEN n % 20 = 0 THEN 0 ELSE 200 + (n % 400) END,
    CASE WHEN n % 20 = 0 THEN 64 ELSE 800 + (n % 1600) END,
    CASE
        WHEN n % 25 = 0  THEN 500
        WHEN n % 33 = 0  THEN 400
        WHEN n % 50 = 0  THEN 404
        WHEN n % 77 = 0  THEN 401
        ELSE 200
    END,
    CASE
        WHEN n % 10 < 4  THEN 30 + (n % 40)
        WHEN n % 10 < 6  THEN 150 + (n % 100)
        WHEN n % 20 = 0  THEN 1 + (n % 3)
        ELSE 10 + (n % 30)
    END,
    NULL,
    -- Spread 400 entries from 2025-12-24 to 2026-03-20 (~87 days = 7,516,800 seconds)
    -- Each entry ~18,792 seconds apart (~5.2 hours)
    strftime('%Y-%m-%dT%H:%M:%S.0000000Z',
        julianday('2025-12-24T00:00:00Z') + ((n - 101) * 87.0 / 400.0)
    ),
    strftime('%Y-%m-%dT%H:%M:%S.0000000Z',
        julianday('2025-12-24T00:00:00Z') + ((n - 101) * 87.0 / 400.0) + (0.001 * (30 + (n % 40)) / 86400.0)
    )
FROM gen;
