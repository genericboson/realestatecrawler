# realestatecrawler

에이스하이엔드 성수타워(서울 성동구 성수이로10길 14) 근처의 **신규 월세 매물**을 다방·직방에서 1시간마다 수집해,
이전에 없던 매물만 문자(SMS/LMS)로 알려 주는 Windows 서비스입니다. C# / .NET 10.

## 조건

| 항목 | 값 | 설정 키 (`appsettings.json` 의 `Crawler`) |
|---|---|---|
| 중심 | 37.540131, 127.05679 (에이스하이엔드 성수타워) | `CenterLat`, `CenterLng` |
| 반경 | 1km (직선거리) | `RadiusKm` |
| 거래 | 월세 (반전세 포함) | — |
| 유형 | 원룸, 투룸, 오피스텔, 아파트 | — |
| 층 | 2층 이상 | `MinFloor` |
| 주기 | 60분 | `IntervalMinutes` |

수집하는 카테고리는 7개입니다.

- 다방: 원룸·투룸, 오피스텔, 아파트
- 직방: 원룸, 빌라·투룸(쓰리룸 이상 제외), 오피스텔, 아파트

## 신규 판정과 문자

- **처음 수집은 문자를 보내지 않습니다.** 카테고리마다 첫 성공 수집을 기준선으로 저장합니다.
- 두 번째 수집부터는 한 번도 본 적 없는 매물만 모아 문자로 보냅니다.
- 한 카테고리가 실패해도 그 회차에서 빠질 뿐입니다. 복구된 뒤 기존 매물이 한꺼번에 "신규"로 쏟아지지 않습니다.
- 문자 발송이 실패하면 그 매물은 본 것으로 기록하지 않고 다음 회차에 다시 보냅니다.
- 다방과 직방에 같은 집이 올라와 있으면 두 건으로 옵니다 (사이트 간 같은 매물을 확실히 가려낼 방법이 없음).
- 한 회차의 문자는 최대 5통(`MaxMessagesPerRun`)이고, 넘치는 매물은 마지막 문자에 "외 N건"으로 적고 로그에 모두 남깁니다.

문자 예시:

```
[성수 신규 월세 2건]

[다방] 투룸 월세 1000/120
자양동 · 3층 · 42.97㎡
https://www.dabangapp.com/room/6ad72e144de906276e96dbb8

[직방] 오피스텔 월세 1000/105
성동구 성수동2가 · 13층 · 33.45㎡
https://www.zigbang.com/home/officetel/items/50670823
```

## 문자 발송 설정 (SOLAPI)

문자는 [SOLAPI](https://solapi.com)(구 CoolSMS) API 로 보냅니다. 건당 요금이 있습니다 (매물 링크가 들어가 대부분 LMS 로 나감).

1. SOLAPI 가입 후 잔액을 충전합니다.
2. **발신번호 등록**: 콘솔에서 본인 휴대폰 번호를 발신번호로 등록합니다 (본인 인증). 받는 번호와 같아도 됩니다.
3. **API Key 발급**: 콘솔 → 개발/연동 → API Key 에서 API Key 와 API Secret 을 만듭니다.
4. `src/RealEstateCrawler/appsettings.Local.example.json` 을 같은 폴더의 `appsettings.Local.json` 으로 복사하고 값을 채웁니다.

```json
{
  "Sms": {
    "ApiKey": "발급받은 API Key",
    "ApiSecret": "발급받은 API Secret",
    "From": "등록한 발신번호",
    "To": "받을 번호"
  }
}
```

`appsettings.Local.json` 은 `.gitignore` 에 들어 있어 커밋되지 않습니다. 전화번호와 키는 이 파일에만 둡니다.
키가 비어 있으면 크롤러는 문자 대신 보낼 내용을 로그에 경고로 남깁니다.

## 실행

```powershell
# 지금 조건에 맞는 매물을 한 번 출력만 (상태 저장·문자 없음)
dotnet run --project src/RealEstateCrawler -- --dry-run

# 설정한 번호로 시험 문자 한 통
dotnet run --project src/RealEstateCrawler -- --test-sms

# 단위 테스트
dotnet test
```

### Windows 서비스로 상시 실행

관리자 PowerShell 에서:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\install-service.ps1
```

- `C:\Program Files\RealEstateCrawler` 에 배포하고 `RealEstateCrawler` 서비스를 자동 시작으로 등록합니다.
- 프로세스가 죽으면 1분 뒤 자동으로 다시 띄웁니다.
- 코드나 `appsettings.Local.json` 을 바꾼 뒤 같은 스크립트를 다시 돌리면 갱신됩니다.
- 제거: `scripts\uninstall-service.ps1`

상태와 로그는 `C:\ProgramData\RealEstateCrawler` 에 있습니다.

- `state.json`: 기준선을 잡은 카테고리, 본 매물과 마지막으로 본 시각 (30일 동안 안 보이면 지움)
- `logs\crawler-YYYYMMDD.log`: 회차별 수집 건수, 신규 매물, 오류

`state.json` 을 지우면 다음 회차가 다시 "첫 수집"이 되어 문자 없이 기준선만 잡습니다.

## 동작 방식

두 사이트 모두 공식 API 가 없어 웹사이트가 쓰는 내부 API 를 그대로 호출합니다. 사이트가 바뀌면 깨질 수 있고,
그때는 로그에 `수집 실패` 가 남습니다.

- **다방**: `api/v5/room-list/category/{one-two|officetel|apt}/bbox`. 웹 클라이언트 헤더(`csrf`, `D-Api-Version` 등)가 필요합니다.
  원룸·투룸은 서버 필터로 2층 이상·옥탑만 받고, 나머지는 받은 뒤 층을 거릅니다.
- **직방 원룸·빌라·오피스텔**: `house/property/v1/items/{onerooms|villas|officetels}` 로 geohash(5자리) 셀마다 매물 번호와 좌표를 받고,
  반경 안 번호만 `items/list`(한 번에 15건)로 상세를 읽습니다. 직방 웹은 geohash 를 AES-256-CBC 로 암호화해 보내므로 같은 방식으로
  암호화합니다. 키는 직방 웹 번들에 들어 있는 값이며, 직방이 키를 바꾸면 `ZigbangGeohashKey` 만 고치면 됩니다.
- **직방 아파트**: `apt/nearby-danjis` 로 반경 안 단지를 찾고 단지마다 `apt/danjis/{id}/item-catalogs`(월세)를 읽습니다.
  링크는 단지 페이지로 연결됩니다.

요청 사이에 0.25초씩 쉬며, 한 회차는 보통 20초 안팎에 끝납니다 (직방 아파트는 반경 안 단지 수만큼 요청).

## 자동 결정 기록 (2026-10-27)

요청에 명시되지 않아 아래처럼 정했습니다. 모두 설정이나 코드 한 곳만 바꾸면 됩니다.

- **"부근" = 반경 1km**: 도보 15분 남짓. `RadiusKm` 으로 조절.
- **"신규" = 지금까지 본 적 없는 매물**: 직전 회차만 비교하면 지도 경계나 목록 흔들림으로 사라졌다 다시 보이는 매물이 반복해서 문자로 옵니다.
  그래서 최근 30일 안에 한 번이라도 본 매물은 다시 보내지 않습니다.
- **층 판정**: 숫자 층은 2 이상만, 지하·반지하는 제외, 옥탑은 포함.
  사이트가 정확한 층을 숨기는 "저층/중층/고층"과 층 정보가 없는 매물은 놓치지 않도록 포함하고 문자에 그 표기를 그대로 적습니다.
- **투룸 범위**: 다방 원룸·투룸 카테고리, 직방 원룸·빌라의 투룸(room_type 04)까지. 쓰리룸 이상 빌라는 제외.
  오피스텔·아파트는 방 개수와 관계없이 포함.
- **문자 서비스 = SOLAPI**: 국내 개발자용 문자 API 중 가입·연동이 가장 간단하고 본인 번호를 발신번호로 쓸 수 있습니다.
- **문자 한도 = 회차당 5통**: 사이트 구조가 바뀌어 모든 매물이 신규로 보이는 사고가 나도 요금 폭탄이 나지 않게.
- **실행 계정 = LocalSystem**, 상태는 `%ProgramData%`.
