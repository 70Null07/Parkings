АННОТАЦИЯ
к магистерской диссертации

Разработка платформы для мониторинга состояния
парковочных мест по видеоданным

Ключевые слова: парковки, мониторинг, обнаружение автомобилей, ASP.NET Aspire, Worker Service YOLO26-SEG, SMTP, SSL-сертификат, обучение моделей, Яндекс.Карты, REST API, ведение журнала.
В магистерской диссертации рассматривается разработка платформы для мониторинга парковок по видеопоследовательности, которая обрабатывает видео с камер наружного видеонаблюдения, записывает информацию в базу данных и позволяет просматривать загруженность парковок для пользователей, предоставляет статистику для владельцев парковок.
В работе рассматриваются особенности существующих решений в области мониторинга парковок, технологии, применяемые для распознавания автомобилей, описывается структура платформы из 5 компонентов, проводится проектирование базы данных и методов предобработки изображения и приводится результат программной реализации всех составляющих.

ABSTRACT
to the master’s thesis

Development of a platform for monitoring the status of parking spaces
using video data
Keywords: parking lots, monitoring, car detection, ASP.NET Aspire, Worker Service YOLO26-SEG, SMTP, SSL certificate, model training, Yandex.Maps, REST API, logging.
The master’s thesis considers the development of a platform for parking lot monitoring using video sequences, which processes video from outdoor surveillance cameras, records information in a database, and allows users to view parking occupancy, providing statistics for parking lot owners.
The work examines the features of existing solutions in the field of parking monitoring, technologies used for car recognition, describes the structure of the platform consisting of 5 components, includes the design of the database and image preprocessing methods, and presents the result of the software implementation of all components.


Схема взаимодействия компонентов приведена на рисунке.
<img width="1342" height="714" alt="image" src="https://github.com/user-attachments/assets/e0ea3a5c-2934-409d-b2df-0318f6673b94" />


Общая структура сервисного приложения приведена на рисунке
<img width="905" height="1478" alt="image" src="https://github.com/user-attachments/assets/35af4c08-285c-4d5f-ab8d-c6ad552dae6d" />


Предобработка затемненных изображений приведена на рисунке.
<img width="1004" height="378" alt="image" src="https://github.com/user-attachments/assets/80c41572-2363-43d4-9190-d01277be5bdf" />


Страница с картой сайта приведена на рисунке.
<img width="1004" height="460" alt="image" src="https://github.com/user-attachments/assets/498c9b9e-1580-4c3f-82a8-d0a79640fb8d" />


Индивидуальная страница парковки приведена на рисунке.
<img width="1004" height="605" alt="image" src="https://github.com/user-attachments/assets/4b005b8a-cec8-4811-aa04-32b9b39a388d" />

Режим разметки.
<img width="1004" height="629" alt="image" src="https://github.com/user-attachments/assets/a1910988-0cb8-44a2-8bbc-0fd0335bfdb4" />

Блокировка мест, доступность видеопотока и алармы.
<img width="1004" height="430" alt="image" src="https://github.com/user-attachments/assets/334ced7f-6f7f-4bfe-bc4b-ff843e68bb17" />

Статистика.
<img width="1004" height="501" alt="image" src="https://github.com/user-attachments/assets/4e7f04f0-467f-49a0-b1e7-d4e450f6cba3" />

Административные страницы.
<img width="1004" height="343" alt="image" src="https://github.com/user-attachments/assets/6219b40c-cdc9-4706-9c12-1be91ae71b54" />
<img width="1004" height="331" alt="image" src="https://github.com/user-attachments/assets/8217ae44-11d8-4973-bc19-509b95f6bcb5" />
<img width="1004" height="399" alt="image" src="https://github.com/user-attachments/assets/0b2891f2-c482-4805-bf60-14db2952ab7d" />

Тестирование детекции.
<img width="1004" height="369" alt="image" src="https://github.com/user-attachments/assets/53ff0865-2212-47cd-add7-4c945b5ab23c" />
<img width="1004" height="370" alt="image" src="https://github.com/user-attachments/assets/f697c8ef-9b79-4146-b856-bd5541cd12ce" />
<img width="1004" height="268" alt="image" src="https://github.com/user-attachments/assets/f9e2b549-c6c5-4c17-8d5e-a2a5f6555e1c" />
<img width="1003" height="191" alt="image" src="https://github.com/user-attachments/assets/08a9b89d-95f0-43ce-8667-a64af3cbabeb" />

В результате выполнения магистерской диссертации разработана платформа для мониторинга парковок по видеопоследовательности, поддерживающая разграничение доступа.
На начальном этапе был рассмотрен процесс работы парковок и выделены аспекты автоматизации, затем рассмотрены технологии обнаружения объектов по видеопоследовательности и методы коррекции освещенности изображений, после этого описаны технологии разработки веб-приложений. Далее проведен обзор существующих решений – «iCCTV», «ParkInto», «Key Parklio PMS», «Genetec AutoVu», «Hikvision Parking Management Solution», «Parkable». Рассмотрены технологии почтовых серверов и наборы данных для обучения моделей для улучшения детекции. С учетом результатов сформированы требования к разрабатываемой платформе. Проведен обзор технологий и алгоритмов, которые лучше всего подходят для реализации платформы. На основе него выбран Aspire на платформе ASP.NET Core 10, Entity Framework для работы с базой данных и аутентификация на основе JWT-токенов.
После этого была разработана структура платформы и её компонентов для обработки видеопоследовательности. Проведено проектирование структуры базы данных и Api-сервиса, взаимодействующего с ней. Разработана структура сетевого приложения и его модулей. Разработан фоновый сервис, обеспечивающий обработку видеопотоков форматов RTSP, MJPEG и JPG с камер видеонаблюдения. Проверены методы предобработки изображений на затемненных изображениях. В качестве основного инструмента компьютерного зрения выбраны модели семейства YOLO26.  Выполнено обучение моделей для детекции автомобилей с различных ракурсов, включая классический и вид с высоты.
На основе разработанной архитектуры платформы были реализованы классы для приложения, получающего и обрабатывающего видеофрагменты. Сформировано описание методов обработки данных. Проведено нагрузочное тестирование веб-приложения и выполнена оценка точности моделей, которая показала: точность распознавания автомобилей для светлого времени суток в хороших условиях 0.884, для плохих погодных условий (ночь, дождь) от 0.543 до 0.714.
Разработанное веб-приложение обладает простым пользовательским интерфейсом и не требует дополнительного обучения для его применения. Платформа позволяет пользователям просматривать видеопоток и загруженность парковок, а владельцам парковок получать статистическую информацию.


Нагрузочное тестирование.
<img width="1004" height="179" alt="image" src="https://github.com/user-attachments/assets/9ba21ee3-b798-4f2d-8275-b60bb7793a4d" />
<img width="1004" height="165" alt="image" src="https://github.com/user-attachments/assets/f13c4d98-602a-43d1-9a22-f0cda2d91a86" />
<img width="1004" height="166" alt="image" src="https://github.com/user-attachments/assets/82efbc7d-b9da-4e08-801e-2a899873e781" />
