import http from 'k6/http';
import { check } from 'k6';

export const options = {
    stages: [
        { duration: '10s', target: 5 },
        { duration: '20s', target: 10 },
        { duration: '10s', target: 0 },
    ],
    thresholds: {
        http_req_failed: ['rate<0.01'],
        http_req_duration: ['p(95)<1000'],
    },
};

export default function () {
    const response = http.get('http://localhost:5110/api/Products');

    check(response, {
        'status is 200': (r) => r.status === 200,
        'response is not empty': (r) => r.body.length > 0,
    });
}